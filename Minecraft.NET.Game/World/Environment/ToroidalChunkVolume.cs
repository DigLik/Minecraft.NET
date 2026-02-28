using System.Runtime.CompilerServices;

using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Environment;

public sealed class ToroidalChunkVolume
{
    private readonly ChunkSection[] _chunks;
    private readonly Vector3<int>[] _positions;
    private readonly Lock[] _locks;

    private readonly int _width;
    private readonly int _height;
    private readonly int _depth;

    public ToroidalChunkVolume(int renderDistance, int worldHeightChunks)
    {
        _width = (renderDistance * 2) + 4;
        _height = worldHeightChunks;
        _depth = (renderDistance * 2) + 4;

        int volume = _width * _height * _depth;
        _chunks = new ChunkSection[volume];
        _positions = new Vector3<int>[volume];
        _locks = new Lock[volume];

        for (int i = 0; i < volume; i++)
        {
            _locks[i] = new Lock();
            _positions[i] = new Vector3<int>(int.MaxValue);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(Vector3<int> pos)
    {
        int x = (pos.X % _width + _width) % _width;
        int y = (pos.Y % _height + _height) % _height;
        int z = (pos.Z % _depth + _depth) % _depth;
        return x + _width * (y + _height * z);
    }

    public bool TryGetChunk(Vector3<int> pos, out ChunkSection chunk)
    {
        int index = GetIndex(pos);
        lock (_locks[index])
        {
            if (_positions[index] == pos)
            {
                chunk = _chunks[index];
                return true;
            }
        }
        chunk = default;
        return false;
    }

    public ChunkSection? SetChunk(Vector3<int> pos, ChunkSection newChunk)
    {
        int index = GetIndex(pos);
        lock (_locks[index])
        {
            ChunkSection? evictedChunk = null;

            if (_positions[index] != new Vector3<int>(int.MaxValue) && _positions[index] != pos)
                evictedChunk = _chunks[index];

            _chunks[index] = newChunk;
            _positions[index] = pos;

            return evictedChunk;
        }
    }

    public delegate void ChunkUpdateAction(ref ChunkSection chunk);

    public void UpdateChunk(Vector3<int> pos, ChunkUpdateAction action)
    {
        int index = GetIndex(pos);
        lock (_locks[index])
            if (_positions[index] == pos)
                action(ref _chunks[index]);
    }

    public void RemoveChunk(Vector3<int> pos, out ChunkSection chunk)
    {
        int index = GetIndex(pos);
        lock (_locks[index])
        {
            if (_positions[index] == pos)
            {
                chunk = _chunks[index];
                _positions[index] = new Vector3<int>(int.MaxValue);
                _chunks[index] = default;
                return;
            }
        }
        chunk = default;
    }

    public IEnumerable<ChunkSection> GetAllValidChunks()
    {
        for (int i = 0; i < _chunks.Length; i++)
            if (_positions[i] != new Vector3<int>(int.MaxValue))
                yield return _chunks[i];
    }
}