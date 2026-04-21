using Minecraft.NET.Engine.Abstractions.Graphics;
using Minecraft.NET.Utils.Math;

namespace Minecraft.NET.Game.World.Meshing;

public class PriorityChunkQueue
{
    private readonly HashSet<Vector3Int>[] _buckets;
    private readonly HashSet<Vector3Int> _hashed = [];
    private int _count = 0;
    private readonly object _sync = new();
    private bool _completed;

    private Vector3Int _playerChunk;

    public PriorityChunkQueue(int maxDistanceBucket = 128)
    {
        _buckets = new HashSet<Vector3Int>[maxDistanceBucket];
        for (int i = 0; i < _buckets.Length; i++)
            _buckets[i] = [];
    }

    public void UpdateCamera(in CameraData camera)
    {
        lock (_sync)
        {
            if (camera.ChunkPosition != _playerChunk)
            {
                _playerChunk = camera.ChunkPosition;
                RebuildBucketsLocked();
            }
        }
    }

    private void RebuildBucketsLocked()
    {
        if (_count == 0) return;
        List<Vector3Int> all = new List<Vector3Int>(_count);
        for (int i = 0; i < _buckets.Length; i++)
        {
            if (_buckets[i].Count > 0)
            {
                all.AddRange(_buckets[i]);
                _buckets[i].Clear();
            }
        }

        foreach (var pos in all)
        {
            int dist = System.Math.Max(System.Math.Abs(pos.X - _playerChunk.X), System.Math.Abs(pos.Y - _playerChunk.Y));
            if (dist >= _buckets.Length) dist = _buckets.Length - 1;
            _buckets[dist].Add(pos);
        }
    }

    public void Add(Vector3Int pos)
    {
        lock (_sync)
        {
            if (_completed) return;

            if (_hashed.Add(pos))
            {
                int dist = System.Math.Max(System.Math.Abs(pos.X - _playerChunk.X), System.Math.Abs(pos.Y - _playerChunk.Y));
                if (dist >= _buckets.Length) dist = _buckets.Length - 1;

                _buckets[dist].Add(pos);
                _count++;
                Monitor.Pulse(_sync);
            }
        }
    }

    public bool Remove(Vector3Int pos)
    {
        lock (_sync)
        {
            if (_hashed.Remove(pos))
            {
                for (int i = 0; i < _buckets.Length; i++)
                {
                    if (_buckets[i].Remove(pos))
                    {
                        _count--;
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public bool TryTake(out Vector3Int item, bool block)
    {
        lock (_sync)
        {
            while (_count == 0)
            {
                if (_completed || !block)
                {
                    item = default;
                    return false;
                }
                Monitor.Wait(_sync);
            }

            for (int i = 0; i < _buckets.Length; i++)
            {
                if (_buckets[i].Count > 0)
                {
                    var fb = _buckets[i].First();
                    _buckets[i].Remove(fb);
                    _hashed.Remove(fb);
                    _count--;
                    item = fb;
                    return true;
                }
            }

            item = default;
            return false;
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
