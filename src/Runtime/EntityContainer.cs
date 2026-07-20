namespace OrionContainers;

using Orion.Api;
using Orion.Api.Items;
using Orion.Api.Network;
using Orion.Containers;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;

public sealed class EntityContainer : Container
{
    public IEntity Entity { get; }

    public EntityContainer(IEntity entity, ContainerType type, int size) : base(type, size)
    {
        Entity = entity;
    }

    public bool IsOwnedBy(IPlayer player) => ReferenceEquals(Entity, player);

    public override void SetItem(int slot, IItemStack item)
    {
        base.SetItem(slot, item);
    }

    public override void UpdateSlot(int slot)
    {
        Entity.NotifyContainerUpdate(this);
        if (slot < 0 || slot >= GetSize())
        {
            base.UpdateSlot(slot);
            return;
        }

        if (Entity is IPlayer player && Identifier == 0 && player.Spawned)
        {
            player.Send(new OpaqueOutboundPacket(new InventorySlotPacket
            {
                WindowId = (uint)(Identifier ?? 0),
                Slot = (uint)slot,
                Container = new Optional<FullContainerName>
                {
                    HasValue = true,
                    Value = new FullContainerName { ContainerId = (byte)ContainerId.Inventory }
                },
                NewItem = ToItemInstanceNew(GetItem(slot))
            }));
        }

        base.UpdateSlot(slot);
    }

    public override void Update()
    {
        Entity.NotifyContainerUpdate(this);
        base.Update();
    }

    protected override long GetContainerEntityUniqueId() => Entity.UniqueId;

    protected override BlockPos GetContainerPosition()
    {
        if (Entity.IsPlayer())
        {
            return new BlockPos { X = 0, Y = 0, Z = 0 };
        }

        return new BlockPos
        {
            X = (int)MathF.Floor(Entity.Position.X),
            Y = (int)MathF.Floor(Entity.Position.Y),
            Z = (int)MathF.Floor(Entity.Position.Z)
        };
    }

    protected override bool CanOpen(IPlayer player, int windowId) => true;

    protected override byte GetFullContainerNameId()
    {
        if (Identifier == 124)
        {
            return (byte)ContainerName.Cursor;
        }

        return base.GetFullContainerNameId();
    }
}
