namespace Minecraft.NET.Engine.ECS;

public readonly struct EntityBuilder(Registry registry, Entity entity)
{
    public Entity Entity => entity;

    public EntityBuilder With<T>(in T component)
    {
        registry.AddComponent(entity, in component);
        return this;
    }

    public static implicit operator Entity(EntityBuilder builder) => builder.Entity;
}