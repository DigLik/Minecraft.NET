using System.Collections.Concurrent;

using HighPerformanceBus;

using Minecraft.NET.Engine.Abstractions;
using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Engine.Core;
using Minecraft.NET.Engine.ECS;
using Minecraft.NET.Game.Entities;
using Minecraft.NET.Game.Events;
using Minecraft.NET.Game.World.Blocks;
using Minecraft.NET.Utils.Math;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using GameWorld = Minecraft.NET.Game.World.Environment.World;

namespace Minecraft.NET.Game.World.Meshing;

public class ChunkRenderSystem : ISystem, IDisposable, IEventHandler<BlockChangedEvent>
{
    private readonly IRenderPipeline _pipeline;
    private readonly GameWorld _world;
    private readonly ChunkMesher _mesher;

    private readonly Dictionary<Vector3<int>, IMesh> _meshes = [];

    private readonly ConcurrentDictionary<Vector3<int>, byte> _activeChunks = new();
    private readonly ConcurrentDictionary<Vector3<int>, byte> _loadedChunks = new();
    private readonly ConcurrentDictionary<Vector3<int>, byte> _meshedChunks = new();

    private readonly ConcurrentQueue<Vector3<int>> _loadQueue = new();
    private readonly ConcurrentQueue<Vector3<int>> _meshQueue = new();
    private readonly ConcurrentQueue<(Vector3<int> Pos, IMesh? Mesh)> _builtMeshes = new();

    private Vector3<int> _lastPlayerChunk = new(int.MaxValue);
    private ITextureArray? _textureArray;

    private bool _isDisposed;
    private readonly Thread[] _workers;

    public ChunkRenderSystem(IRenderPipeline pipeline, GameWorld world)
    {
        _pipeline = pipeline;
        _world = world;
        _mesher = new ChunkMesher(world.Chunks);

        EventBus.Subscribe(this);
        LoadTextures();

        _workers = new Thread[Math.Max(1, System.Environment.ProcessorCount / 2)];
        for (int i = 0; i < _workers.Length; i++)
        {
            _workers[i] = new Thread(WorkerLoop) { IsBackground = true };
            _workers[i].Start();
        }
    }

    private void LoadTextures()
    {
        var files = BlockRegistry.TextureFiles;
        int size = 16;
        byte[][] pixels = new byte[files.Count][];

        for (int i = 0; i < files.Count; i++)
        {
            string path = files[i];
            pixels[i] = new byte[size * size * 4];

            if (File.Exists(path))
            {
                using var image = Image.Load<Rgba32>(path);
                image.CopyPixelDataTo(pixels[i]);
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

    private void MarkForRemesh(Vector3<int> pos)
    {
        if (!_activeChunks.ContainsKey(pos)) return;

        bool IsLoadedOrOOB(Vector3<int> p) => p.Z < 0 || p.Z >= WorldHeightInChunks || !_activeChunks.ContainsKey(p) || _loadedChunks.ContainsKey(p);

        if (IsLoadedOrOOB(pos + new Vector3<int>(1, 0, 0)) &&
            IsLoadedOrOOB(pos + new Vector3<int>(-1, 0, 0)) &&
            IsLoadedOrOOB(pos + new Vector3<int>(0, 1, 0)) &&
            IsLoadedOrOOB(pos + new Vector3<int>(0, -1, 0)) &&
            IsLoadedOrOOB(pos + new Vector3<int>(0, 0, 1)) &&
            IsLoadedOrOOB(pos + new Vector3<int>(0, 0, -1)))
        {
            _meshedChunks.TryRemove(pos, out _);
            if (_meshedChunks.TryAdd(pos, 0))
                _meshQueue.Enqueue(pos);
        }
    }

    private void WorkerLoop()
    {
        while (!_isDisposed)
        {
            if (_loadQueue.TryDequeue(out var loadPos))
            {
                if (!_activeChunks.ContainsKey(loadPos)) continue;

                _world.Chunks.LoadChunk(loadPos);
                _loadedChunks.TryAdd(loadPos, 0);

                MarkForRemesh(loadPos);
                MarkForRemesh(loadPos + new Vector3<int>(1, 0, 0));
                MarkForRemesh(loadPos + new Vector3<int>(-1, 0, 0));
                MarkForRemesh(loadPos + new Vector3<int>(0, 1, 0));
                MarkForRemesh(loadPos + new Vector3<int>(0, -1, 0));
                MarkForRemesh(loadPos + new Vector3<int>(0, 0, 1));
                MarkForRemesh(loadPos + new Vector3<int>(0, 0, -1));
            }
            else if (_meshQueue.TryDequeue(out var meshPos))
            {
                if (!_activeChunks.ContainsKey(meshPos)) continue;

                var meshData = _mesher.GenerateMesh(meshPos);

                if (!meshData.IsEmpty)
                {
                    var gpuMesh = _pipeline.CreateMesh(meshData.Vertices, meshData.Indices);
                    _builtMeshes.Enqueue((meshPos, gpuMesh));
                }
                else
                {
                    _builtMeshes.Enqueue((meshPos, null));
                }
            }
            else
            {
                Thread.Sleep(2);
            }
        }
    }

    public void Handle(in BlockChangedEvent @event)
    {
        Vector3<int> chunkPos = new(
            @event.GlobalPosition.X >> 4,
            @event.GlobalPosition.Y >> 4,
            @event.GlobalPosition.Z >> 4
        );

        MarkForRemesh(chunkPos);

        int lx = @event.GlobalPosition.X & 15;
        int ly = @event.GlobalPosition.Y & 15;
        int lz = @event.GlobalPosition.Z & 15;

        if (lx == 0) MarkForRemesh(chunkPos + new Vector3<int>(-1, 0, 0));
        if (lx == 15) MarkForRemesh(chunkPos + new Vector3<int>(1, 0, 0));
        if (ly == 0) MarkForRemesh(chunkPos + new Vector3<int>(0, -1, 0));
        if (ly == 15) MarkForRemesh(chunkPos + new Vector3<int>(0, 1, 0));
        if (lz == 0) MarkForRemesh(chunkPos + new Vector3<int>(0, 0, -1));
        if (lz == 15) MarkForRemesh(chunkPos + new Vector3<int>(0, 0, 1));
    }

    public void Update(Registry registry, in Time time)
    {
        if (_textureArray != null)
            _pipeline.BindTextureArray(_textureArray);

        Vector3<int> playerChunkPos = default;
        bool hasPlayer = false;

        foreach (var item in registry.GetView<TransformComponent>())
        {
            playerChunkPos = new Vector3<int>(
                (int)MathF.Floor(item.Comp1.Position.X / ChunkSize),
                (int)MathF.Floor(item.Comp1.Position.Y / ChunkSize),
                (int)MathF.Floor(item.Comp1.Position.Z / ChunkSize)
            );
            hasPlayer = true;
            break;
        }

        if (hasPlayer && playerChunkPos != _lastPlayerChunk)
        {
            _lastPlayerChunk = playerChunkPos;
            UpdateRenderDistance(playerChunkPos);
        }

        int uploadedThisFrame = 0;
        while (uploadedThisFrame < 64 && _builtMeshes.TryDequeue(out var result))
        {
            if (!_activeChunks.ContainsKey(result.Pos))
            {
                if (result.Mesh != null) _pipeline.DeleteMesh(result.Mesh);
                continue;
            }

            if (_meshes.TryGetValue(result.Pos, out var oldMesh))
            {
                _pipeline.DeleteMesh(oldMesh);
                _meshes.Remove(result.Pos);
            }

            if (result.Mesh != null)
            {
                _meshes[result.Pos] = result.Mesh;
            }

            uploadedThisFrame++;
        }

        foreach (var kvp in _meshes)
        {
            Vector3<float> position = new(kvp.Key.X * ChunkSize, kvp.Key.Y * ChunkSize, kvp.Key.Z * ChunkSize);
            _pipeline.SubmitDraw(kvp.Value, position);
        }
    }

    private void UpdateRenderDistance(Vector3<int> center)
    {
        for (int x = center.X - RenderDistance; x <= center.X + RenderDistance; x++)
        {
            for (int y = center.Y - RenderDistance; y <= center.Y + RenderDistance; y++)
            {
                for (int z = 0; z < WorldHeightInChunks; z++)
                {
                    var pos = new Vector3<int>(x, y, z);
                    if (_activeChunks.TryAdd(pos, 0))
                    {
                        _loadQueue.Enqueue(pos);
                    }
                }
            }
        }

        var toRemove = new List<Vector3<int>>();
        foreach (var pos in _activeChunks.Keys)
        {
            if (Math.Abs(pos.X - center.X) > RenderDistance || Math.Abs(pos.Y - center.Y) > RenderDistance)
                toRemove.Add(pos);
        }

        foreach (var pos in toRemove)
        {
            _activeChunks.TryRemove(pos, out _);
            _loadedChunks.TryRemove(pos, out _);
            _meshedChunks.TryRemove(pos, out _);
            if (_meshes.TryGetValue(pos, out var mesh))
            {
                _pipeline.DeleteMesh(mesh);
                _meshes.Remove(pos);
            }
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        EventBus.Unsubscribe(this);
        foreach (var mesh in _meshes.Values) _pipeline.DeleteMesh(mesh);
        _textureArray?.Dispose();
    }
}