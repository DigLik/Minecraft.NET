using System.Numerics;

using HighPerformanceBus;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Engine.ECS;
using Minecraft.NET.Game.Entities;
using Minecraft.NET.Game.Events;
using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Utils.Collections;
using Minecraft.NET.Utils.Math;

using ZstdSharp;

using GameWorld = Minecraft.NET.Game.World.Environment.World;

namespace Minecraft.NET.Game.World.Meshing;

public class ChunkRenderSystem : ISystem, IDisposable, IEventHandler<BlockChangedEvent>
{
    private readonly IRenderPipeline _pipeline;
    private readonly GameWorld _world;
    private readonly ChunkMesher _mesher;

    private readonly ZeroAllocQueue<Vector3Int> _loadQueue;
    private readonly ZeroAllocQueue<Vector3Int> _meshQueue;
    private readonly ZeroAllocQueue<(Vector3Int Pos, IMesh? Mesh)> _builtMeshes;

    private readonly Dictionary<Vector3Int, IMesh> _meshes;
    private readonly Dictionary<Vector3Int, IMesh> _pendingReadyMeshes;
    private readonly HashSet<Vector3Int> _activeChunks;
    private readonly HashSet<Vector3Int> _loadedChunks;
    private readonly HashSet<Vector3Int> _meshedChunks;

    private readonly Lock _stateLock = new();

    private NativeList<Vector3Int> _chunksToRemove = new(1024);
    private NativeList<Vector3Int> _readyList = new(1024);

    private Vector3Int _lastPlayerChunk = new(int.MaxValue);
    private ITextureArray? _textureArray;
    private readonly Thread[] _genWorkers;
    private readonly Thread[] _meshWorkers;

    public ChunkRenderSystem(IRenderPipeline pipeline, GameWorld world)
    {
        _pipeline = pipeline;
        _world = world;
        _mesher = new ChunkMesher(world.Chunks);

        int volumeX = (RenderDistance * 2) + 4;
        int maxChunks = volumeX * volumeX * WorldHeightInChunks;
        int queueCapacity = maxChunks * 2;

        _loadQueue = new ZeroAllocQueue<Vector3Int>(queueCapacity);
        _meshQueue = new ZeroAllocQueue<Vector3Int>(queueCapacity);
        _builtMeshes = new ZeroAllocQueue<(Vector3Int Pos, IMesh? Mesh)>(queueCapacity);

        _meshes = new(maxChunks);
        _pendingReadyMeshes = new(maxChunks / 4);
        _activeChunks = new(maxChunks);
        _loadedChunks = new(maxChunks);
        _meshedChunks = new(maxChunks);

        EventBus.Subscribe(this);
        LoadTextures();
        _pipeline.BindMaterials([.. BlockRegistry.MaterialConfigs]);

        int cores = System.Environment.ProcessorCount;
        int genThreads = Math.Max(1, (int)(cores * 0.10f));
        int meshThreads = Math.Max(1, (int)(cores * 0.25f));

        _genWorkers = new Thread[genThreads];
        for (int i = 0; i < genThreads; i++)
        {
            _genWorkers[i] = new Thread(GenLoop) { IsBackground = true, Name = $"ChunkGen_{i}" };
            _genWorkers[i].Start();
        }

        _meshWorkers = new Thread[meshThreads];
        for (int i = 0; i < meshThreads; i++)
        {
            _meshWorkers[i] = new Thread(MeshLoop) { IsBackground = true, Name = $"ChunkMesh_{i}" };
            _meshWorkers[i].Start();
        }
    }

    private void LoadTextures()
    {
        var files = BlockRegistry.TextureFiles;
        int size = 16;
        byte[][] pixels = new byte[files.Count][];

        using var decompressor = new Decompressor();
        for (int i = 0; i < files.Count; i++)
        {
            string path = files[i];
            pixels[i] = new byte[size * size * 4];

            if (File.Exists(path))
            {
                byte[] compressed = File.ReadAllBytes(path);
                decompressor.Unwrap(compressed, pixels[i]);
            }
            else
            {
                for (int p = 0; p < pixels[i].Length; p += 4)
                {
                    pixels[i][p] = 255;
                    pixels[i][p + 1] = 0;
                    pixels[i][p + 2] = 255;
                    pixels[i][p + 3] = 255;
                }
            }
        }

        if (files.Count > 0)
            _textureArray = _pipeline.CreateTextureArray(size, size, pixels);
    }

    private void MarkForRemesh(Vector3Int pos)
    {
        lock (_stateLock)
        {
            if (!_activeChunks.Contains(pos)) return;

            bool IsLoadedOrOOB(Vector3Int p) => p.Z < 0 || p.Z >= WorldHeightInChunks || !_activeChunks.Contains(p) || _loadedChunks.Contains(p);

            if (IsLoadedOrOOB(pos + new Vector3Int(1, 0, 0)) &&
                IsLoadedOrOOB(pos + new Vector3Int(-1, 0, 0)) &&
                IsLoadedOrOOB(pos + new Vector3Int(0, 1, 0)) &&
                IsLoadedOrOOB(pos + new Vector3Int(0, -1, 0)) &&
                IsLoadedOrOOB(pos + new Vector3Int(0, 0, 1)) &&
                IsLoadedOrOOB(pos + new Vector3Int(0, 0, -1)))
            {
                _meshedChunks.Remove(pos);
                if (_meshedChunks.Add(pos)) _meshQueue.Add(pos);
            }
        }
    }

    private void GenLoop()
    {
        try
        {
            while (_loadQueue.TryTake(out var loadPos, block: true))
            {
                lock (_stateLock)
                {
                    if (!_activeChunks.Contains(loadPos)) continue;
                }

                _world.Chunks.LoadChunk(loadPos);

                lock (_stateLock)
                {
                    _loadedChunks.Add(loadPos);
                }

                MarkForRemesh(loadPos);
                MarkForRemesh(loadPos + new Vector3Int(1, 0, 0));
                MarkForRemesh(loadPos + new Vector3Int(-1, 0, 0));
                MarkForRemesh(loadPos + new Vector3Int(0, 1, 0));
                MarkForRemesh(loadPos + new Vector3Int(0, -1, 0));
                MarkForRemesh(loadPos + new Vector3Int(0, 0, 1));
                MarkForRemesh(loadPos + new Vector3Int(0, 0, -1));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GenLoop Exited] {ex.Message}");
        }
    }

    private void MeshLoop()
    {
        try
        {
            while (_meshQueue.TryTake(out var meshPos, block: true))
            {
                bool isActive;
                lock (_stateLock) isActive = _activeChunks.Contains(meshPos);
                if (!isActive) continue;

                var meshData = _mesher.GenerateMesh(meshPos);

                if (!meshData.IsEmpty)
                {
                    var gpuMesh = _pipeline.CreateMesh(meshData.Vertices, meshData.Indices);
                    _builtMeshes.Add((meshPos, gpuMesh));
                }
                else
                {
                    _builtMeshes.Add((meshPos, null));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MeshLoop Exited] {ex.Message}");
        }
    }

    public void Handle(in BlockChangedEvent @event)
    {
        Vector3Int chunkPos = new(
            @event.GlobalPosition.X >> 4,
            @event.GlobalPosition.Y >> 4,
            @event.GlobalPosition.Z >> 4
        );
        MarkForRemesh(chunkPos);

        int lx = @event.GlobalPosition.X & 15;
        int ly = @event.GlobalPosition.Y & 15;
        int lz = @event.GlobalPosition.Z & 15;

        if (lx == 0) MarkForRemesh(chunkPos + new Vector3Int(-1, 0, 0));
        if (lx == 15) MarkForRemesh(chunkPos + new Vector3Int(1, 0, 0));
        if (ly == 0) MarkForRemesh(chunkPos + new Vector3Int(0, -1, 0));
        if (ly == 15) MarkForRemesh(chunkPos + new Vector3Int(0, 1, 0));
        if (lz == 0) MarkForRemesh(chunkPos + new Vector3Int(0, 0, -1));
        if (lz == 15) MarkForRemesh(chunkPos + new Vector3Int(0, 0, 1));
    }

    public void Update(Registry registry, in Time time)
    {
        if (_textureArray != null)
            _pipeline.BindTextureArray(_textureArray);

        Vector3Int playerChunkPos = default;
        bool hasPlayer = false;

        foreach (var item in registry.GetView<TransformComponent>())
        {
            playerChunkPos = item.Comp1.ChunkPosition;
            hasPlayer = true;
            break;
        }

        if (hasPlayer && playerChunkPos != _lastPlayerChunk)
        {
            _lastPlayerChunk = playerChunkPos;
            UpdateRenderDistance(playerChunkPos);
        }

        while (_builtMeshes.TryTake(out var result, block: false))
        {
            bool isActive;
            lock (_stateLock) isActive = _activeChunks.Contains(result.Pos);

            if (!isActive)
            {
                if (result.Mesh != null) _pipeline.DeleteMesh(result.Mesh);
                continue;
            }

            lock (_stateLock)
            {
                if (result.Mesh == null)
                {
                    if (_meshes.Remove(result.Pos, out var oldMesh)) _pipeline.DeleteMesh(oldMesh);
                    if (_pendingReadyMeshes.Remove(result.Pos, out var pendingOld)) _pipeline.DeleteMesh(pendingOld);
                }
                else
                {
                    if (_pendingReadyMeshes.Remove(result.Pos, out var oldPending)) _pipeline.DeleteMesh(oldPending);
                    _pendingReadyMeshes[result.Pos] = result.Mesh;
                }
            }
        }

        lock (_stateLock)
        {
            _readyList.Clear();

            foreach (var kvp in _pendingReadyMeshes)
            {
                if (kvp.Value.IsReady)
                {
                    if (_meshes.Remove(kvp.Key, out var oldMesh)) _pipeline.DeleteMesh(oldMesh);
                    _meshes[kvp.Key] = kvp.Value;
                    _readyList.Add(kvp.Key);
                }
            }
            for (int i = 0; i < _readyList.Count; i++) _pendingReadyMeshes.Remove(_readyList[i]);

            foreach (var kvp in _meshes)
            {
                Vector3 position = new(
                    (kvp.Key.X - _lastPlayerChunk.X) * ChunkSize,
                    (kvp.Key.Y - _lastPlayerChunk.Y) * ChunkSize,
                    (kvp.Key.Z - _lastPlayerChunk.Z) * ChunkSize
                );

                _pipeline.SubmitDraw(kvp.Value, position);
            }
        }
    }

    private void UpdateRenderDistance(Vector3Int center)
    {
        lock (_stateLock)
        {
            for (int x = center.X - RenderDistance; x <= center.X + RenderDistance; x++)
            {
                for (int y = center.Y - RenderDistance; y <= center.Y + RenderDistance; y++)
                {
                    for (int z = 0; z < WorldHeightInChunks; z++)
                    {
                        var pos = new Vector3Int(x, y, z);
                        if (_activeChunks.Add(pos))
                            _loadQueue.Add(pos);
                    }
                }
            }

            _chunksToRemove.Clear();

            foreach (var pos in _activeChunks)
            {
                if (Math.Abs(pos.X - center.X) > RenderDistance || Math.Abs(pos.Y - center.Y) > RenderDistance)
                    _chunksToRemove.Add(pos);
            }

            for (int i = 0; i < _chunksToRemove.Count; i++)
            {
                var pos = _chunksToRemove[i];
                _activeChunks.Remove(pos);
                _loadedChunks.Remove(pos);
                _meshedChunks.Remove(pos);

                if (_meshes.Remove(pos, out var mesh)) _pipeline.DeleteMesh(mesh);
                if (_pendingReadyMeshes.Remove(pos, out var pMesh)) _pipeline.DeleteMesh(pMesh);
            }
        }
    }

    public void Dispose()
    {
        EventBus.Unsubscribe(this);

        _loadQueue.CompleteAdding();
        _meshQueue.CompleteAdding();

        foreach (var worker in _genWorkers) worker.Join(TimeSpan.FromSeconds(1));
        foreach (var worker in _meshWorkers) worker.Join(TimeSpan.FromSeconds(1));

        lock (_stateLock)
        {
            foreach (var mesh in _meshes.Values) _pipeline.DeleteMesh(mesh);
            foreach (var mesh in _pendingReadyMeshes.Values) _pipeline.DeleteMesh(mesh);
        }

        _chunksToRemove.Dispose();
        _readyList.Dispose();
        _textureArray?.Dispose();
    }

    private class ZeroAllocQueue<T>(int capacity)
    {
        private readonly T[] _items = new T[capacity];
        private int _head;
        private int _tail;
        private int _count;
        private readonly object _sync = new();
        private bool _completed;

        public void Add(T item)
        {
            lock (_sync)
            {
                while (_count == _items.Length && !_completed)
                {
                    Monitor.Wait(_sync);
                }
                if (_completed) return;

                _items[_tail] = item;
                _tail = (_tail + 1) % _items.Length;
                _count++;
                Monitor.Pulse(_sync);
            }
        }

        public bool TryTake(out T item, bool block)
        {
            lock (_sync)
            {
                while (_count == 0)
                {
                    if (_completed || !block)
                    {
                        item = default!;
                        return false;
                    }
                    Monitor.Wait(_sync);
                }
                item = _items[_head];
                _head = (_head + 1) % _items.Length;
                _count--;
                Monitor.Pulse(_sync);
                return true;
            }
        }

        public void CompleteAdding()
        {
            lock (_sync)
            {
                _completed = true;
                Monitor.PulseAll(_sync);
            }
        }
    }
}