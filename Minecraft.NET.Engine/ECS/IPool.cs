namespace Minecraft.NET.Engine.ECS;

public interface IPool
{
    void Remove(int entityId);
    bool Has(int entityId);
    int Count { get; }
    List<int> EntitiesList { get; }
}