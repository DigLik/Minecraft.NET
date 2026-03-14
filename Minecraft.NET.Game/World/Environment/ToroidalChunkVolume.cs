using System.Runtime.CompilerServices;

using Minecraft.NET.Game.World.Chunks;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Environment;

public sealed class ToroidalChunkVolume
{
    private readonly ChunkSection[] _chunks;
    private readonly Vector3Int[] _positions;
    private readonly Lock[] _locks;

    private readonly int _sizeX;
    private readonly int _sizeY;
    private readonly int _sizeZ;

    public ToroidalChunkVolume(int renderDistance, int worldHeightChunks)
    {
        _sizeX = (renderDistance * 2) + 4;
        _sizeY = (renderDistance * 2) + 4;
        _sizeZ = worldHeightChunks;

        int volume = _sizeX * _sizeY * _sizeZ;

        _chunks = new ChunkSection[volume];
        _positions = new Vector3Int[volume];
        _locks = new Lock[volume];

        for (int i = 0; i < volume; i++)
        {
            _locks[i] = new Lock();
            _positions[i] = new Vector3Int(int.MaxValue);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(Vector3Int pos)
    {
        int x = (pos.X % _sizeX + _sizeX) % _sizeX;
        int y = (pos.Y % _sizeY + _sizeY) % _sizeY;
        int z = (pos.Z % _sizeZ + _sizeZ) % _sizeZ;

        return x + _sizeX * (y + _sizeY * z);
    }

    public bool TryGetChunk(Vector3Int pos, out ChunkSection chunk)
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

    public ChunkSection? SetChunk(Vector3Int pos, ChunkSection newChunk)
    {
        int index = GetIndex(pos);
        lock (_locks[index])
        {
            ChunkSection? evictedChunk = null;

            if (_positions[index] != new Vector3Int(int.MaxValue) && _positions[index] != pos)
                evictedChunk = _chunks[index];

            _chunks[index] = newChunk;
            _positions[index] = pos;

            return evictedChunk;
        }
    }

    public delegate void ChunkUpdateAction(ref ChunkSection chunk);

    public void UpdateChunk(Vector3Int pos, ChunkUpdateAction action)
    {
        int index = GetIndex(pos);
        lock (_locks[index])
            if (_positions[index] == pos)
                action(ref _chunks[index]);
    }

    public void RemoveChunk(Vector3Int pos, out ChunkSection chunk)
    {
        int index = GetIndex(pos);
        lock (_locks[index])
        {
            if (_positions[index] == pos)
            {
                chunk = _chunks[index];
                _positions[index] = new Vector3Int(int.MaxValue);
                _chunks[index] = default;
                return;
            }
        }
        chunk = default;
    }

    public IEnumerable<ChunkSection> GetAllValidChunks()
    {
        for (int i = 0; i < _chunks.Length; i++)
            if (_positions[i] != new Vector3Int(int.MaxValue))
                yield return _chunks[i];
    }
}