namespace Orion.Containers;

using Orion.Api;
using Orion.Api.Network;
using Orion.Protocol.Nbt;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using ApiContainer = Orion.Api.Containers.IContainer;
using ApiContainerType = Orion.Api.Containers.ContainerType;
using IItemStack = Orion.Api.Items.IItemStack;
using WireContainerType = Orion.Containers.ContainerType;

public class Container : ApiContainer
{
    // A list of all the players that are viewing the container
    public Dictionary<IPlayer, int> occupants = [];
    private static int _nextContainerId = 1;

    public WireContainerType Type { get; }
    public int? Identifier { get; set; }
    public List<IItemStack?> Storage { get; private set; }

    public int EmptySlotsCount => Storage.Count(static item => item is null);
    public bool IsFull => EmptySlotsCount == 0;

    ApiContainerType ApiContainer.Type => Type switch
    {
        WireContainerType.Inventory or WireContainerType.Hud => ApiContainerType.Inventory,
        WireContainerType.Hand => ApiContainerType.Hotbar,
        _ => ApiContainerType.Container
    };

    public Container(ContainerType type, int size)
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        Type = type;
        Storage = Enumerable.Repeat<IItemStack?>(null, size).ToList();
    }


    // Returns the size of the container
    public int GetSize()
    {
        return Storage.Count;
    }

    /// <summary>
    /// Sets the size of the container
    /// Please do not use this unless you know what you're doing
    /// This should inseat be used in constructor!
    /// </summary>
    /// <param name="size"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void SetSize(int size)
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        if (size == Storage.Count)
        {
            return;
        }

        List<IItemStack?> resized = Enumerable.Repeat<IItemStack?>(null, size).ToList();
        int copy = Math.Min(size, Storage.Count);
        for (int i = 0; i < copy; i++)
        {
            resized[i] = Storage[i];
        }

        Storage = resized;
        Update();
    }


    /// <summary>
    /// Returns the item in the slot 
    /// But cann also return null
    /// </summary>
    /// <param name="slot"></param>
    /// <returns></returns>
    public IItemStack? GetItem(int slot)
    {
        if (slot < 0 || slot >= Storage.Count)
        {
            return null;
        }
        return Storage[slot];
    }

    /// <summary>
    /// Set an item to a slot.
    /// Will remove the item if the stack size is 0 
    /// but just use the RemoveItem instead if you want to remove the item
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="item"></param>
    public virtual void SetItem(int slot, IItemStack item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (slot < 0 || slot >= Storage.Count)
        {
            return;
        }

        Storage[slot] = item;
        if (item.Count == 0)
        {
            Storage[slot] = null;
        }

        UpdateSlot(slot);
    }

    /// <summary>
    /// Add an item to the container, this checks for slots and counts
    /// so when there is a spot it can put it in it will add, but if there
    /// are no empty slots or stackable slots it will return false
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool AddItem(IItemStack item)
    {
        ArgumentNullException.ThrowIfNull(item);

        for (int i = 0; i < Storage.Count; i++)
        {
            IItemStack? existing = Storage[i];
            if (existing is null || !existing.CanStackWith(item) || existing.Count >= existing.Type.MaxStackSize)
            {
                continue;
            }

            int available = existing.Type.MaxStackSize - existing.Count;
            int move = Math.Min(available, item.Count);
            existing.Increment(move);
            item.Decrement(move);
            UpdateSlot(i);
            if (item.Count == 0)
            {
                return true;
            }
        }

        int empty = Storage.FindIndex(static x => x is null);
        if (empty == -1)
        {
            return false;
        }

        SetItem(empty, item);
        return true;
    }


    /// <summary>
    ///  Removes an item from the container
    /// Returns the removed item or null 
    /// Depending on how many you remove
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public IItemStack? RemoveItem(int slot, int amount)
    {
        if (slot < 0 || slot >= Storage.Count)
        {
            return null;
        }
        if (amount <= 0)
        {
            return null;
        }

        IItemStack? item = Storage[slot];
        if (item is null)
        {
            return null;
        }

        int removed = Math.Min(amount, item.Count);
        item.Decrement(removed);
        if (item.Count == 0)
        {
            Storage[slot] = null;
        }

        UpdateSlot(slot);
        return item;
    }

    /// <summary>
    /// Takes an item from the container
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="amount"></param>
    /// <returns></returns>
    public IItemStack? TakeItem(int slot, int amount)
    {
        if (slot < 0 || slot >= Storage.Count)
        {
            return null;
        }
        if (amount <= 0)
        {
            return null;
        }

        IItemStack? source = Storage[slot];
        if (source is null)
        {
            return null;
        }

        int taken = Math.Min(amount, source.Count);
        if (taken == source.Count)
        {
            Storage[slot] = null;
            UpdateSlot(slot);
            return source;
        }

        source.Decrement(taken);
        UpdateSlot(slot);
        return source.Clone(taken);
    }

    /// <summary>
    /// Swaps items in the container
    /// pretty self explanatory
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="otherSlot"></param>
    /// <param name="otherContainer"></param>
    public void SwapItems(int slot, int otherSlot, Container? otherContainer = null)
    {
        if (slot < 0 || slot >= Storage.Count
            || otherSlot < 0 || otherSlot >= Storage.Count
        )
        {
            return;
        }

        Container target = otherContainer ?? this;

        IItemStack? a = GetItem(slot);
        IItemStack? b = target.GetItem(otherSlot);

        Storage[slot] = b;
        target.Storage[otherSlot] = a;

        UpdateSlot(slot);
        target.UpdateSlot(otherSlot);
    }

    /// <summary>
    /// Clears an item from the container without ifs or buts
    /// </summary>
    /// <param name="slot"></param>
    public virtual void ClearSlot(int slot)
    {
        if (slot < 0 || slot >= Storage.Count)
        {
            return;
        }

        Storage[slot] = null;
        UpdateSlot(slot);
    }

    /// <summary>
    /// Clears the whole container of any items
    /// </summary>
    public virtual void Clear()
    {
        for (int i = 0; i < Storage.Count; i++)
        {
            Storage[i] = null;
        }

        Update();
    }

    internal bool SuppressNetworkSync { get; set; }

    /// <summary>
    /// Updates a single slot of the container and sends it off to player
    /// if they are in the container 
    /// </summary>
    /// <param name="slot"></param>
    public virtual void UpdateSlot(int slot)
    {
        if (Storage.Count == 0)
        {
            return;
        }

        if (slot < 0 || slot >= Storage.Count)
        {
            return;
        }

        if (SuppressNetworkSync)
        {
            return;
        }

        foreach ((IPlayer player, int windowId) in occupants)
        {
            if (!player.Spawned)
            {
                continue;
            }

            InventorySlotPacket packet = new()
            {
                WindowId = (uint)windowId,
                Slot = (uint)slot,
                Container = new Optional<FullContainerName>
                {
                    HasValue = true,
                    Value = GetFullContainerName(windowId)
                },
                NewItem = ToItemInstanceNew(Storage[slot])
            };

            player.Send(new OpaqueOutboundPacket(packet));
        }
    }

    /// <summary>
    /// Updates the whole container and sends it to occupants
    /// </summary>
    public virtual void Update()
    {
        foreach ((IPlayer player, int windowId) in occupants)
        {
            if (!player.Spawned)
            {
                continue;
            }

            InventoryContentPacket packet = new()
            {
                WindowId = (uint)windowId,
                Content = new List<NetworkItemStackDescriptor>(Storage.Count),
                Container = GetFullContainerName(windowId),
                StorageItem = new NetworkItemStackDescriptor()
            };

            for (int i = 0; i < Storage.Count; i++)
            {
                packet.Content.Add(ToItemInstanceNew(Storage[i]));
            }

            player.Send(new OpaqueOutboundPacket(packet));
        }
    }

    /// <summary>
    /// Shows the container to the player
    /// and returns the window id
    /// </summary>
    public virtual int Show(IPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (occupants.TryGetValue(player, out int existing))
        {
            if (player.Spawned && CanOpen(player, existing))
            {
                ContainerOpenPacket openPacket = new()
                {
                    WindowId = (byte)existing,
                    ContainerType = unchecked((byte)(int)Type),
                    ContainerPosition = GetContainerPosition(),
                    ContainerEntityUniqueId = GetContainerEntityUniqueId()
                };

                player.Send(new OpaqueOutboundPacket(openPacket));
                Update();
            }

            return existing;
        }

        int id = Identifier ?? _nextContainerId++;
        occupants[player] = id;
        player.RegisterOpenContainer(id, this);
        OnViewerAdded(player, id);
        if (CanOpen(player, id))
        {
            ContainerOpenPacket openPacket = new()
            {
                WindowId = (byte)id,
                ContainerType = unchecked((byte)(int)Type),
                ContainerPosition = GetContainerPosition(),
                ContainerEntityUniqueId = GetContainerEntityUniqueId()
            };
            if (player.Spawned)
            {
                player.Send(new OpaqueOutboundPacket(openPacket));
            }
        }

        Update();
        return id;
    }

    /// <summary>
    /// Closes the container,
    /// Done as Server to Client.
    /// </summary>
    public virtual void Close(IPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        _ = RemoveViewer(player, true);
    }

    public IReadOnlyCollection<KeyValuePair<IPlayer, int>> GetAllOccupants()
    {
        return occupants;
    }

    /// <summary>
    /// Serializes the container into nbt CompoundTag,
    /// public only so u can override it to store somewhere else
    /// </summary>
    /// <returns></returns>
    public CompoundTag Serialize()
    {
        CompoundTag root = new();
        root.Set("size", new IntTag { Value = GetSize() });

        ListTag items = new() { Name = "items" };
        for (int slot = 0; slot < GetSize(); slot++)
        {
            IItemStack? item = GetItem(slot);
            if (item is null || item.Count == 0)
            {
                continue;
            }

            CompoundTag entry = SerializeItem(item);
            entry.Set("slot", new IntTag { Value = slot });
            items.Values.Add(entry);
        }

        root.Set("items", items);
        return root;
    }

    /// <summary>
    /// Vise versa of Serialize
    /// Deserializes the container data
    /// </summary>
    /// <param name="root"></param>
    public void Deserialize(CompoundTag root)
    {
        int size = root.Get<IntTag>("size")?.Value ?? GetSize();
        if (size != GetSize())
        {
            SetSize(size);
        }

        Clear();
        ListTag? items = root.Get<ListTag>("items");
        if (items is null)
        {
            return;
        }

        for (int i = 0; i < items.Values.Count; i++)
        {
            if (items.Values[i] is not CompoundTag itemTag)
            {
                continue;
            }

            int slot = itemTag.Get<IntTag>("slot")?.Value ?? -1;
            if (slot < 0 || slot >= GetSize())
            {
                continue;
            }

            IItemStack? item = DeserializeItem(itemTag);
            if (item is null || item.Count == 0)
            {
                continue;
            }

            SetItem(slot, item);
        }
    }

    /// <summary>
    /// Builds a minimal NBT representation of an item stack (identifier + count + metadata).
    /// This intentionally does not round-trip item traits/extra data, since those are only
    /// available on the host's concrete item stack implementation.
    /// </summary>
    private static CompoundTag SerializeItem(IItemStack item)
    {
        CompoundTag tag = new();
        tag.Set("id", new StringTag { Value = item.Type.Identifier });
        tag.Set("count", new IntTag { Value = item.Count });
        tag.Set("meta", new IntTag { Value = unchecked((int)item.Metadata) });
        return tag;
    }

    private static IItemStack? DeserializeItem(CompoundTag tag)
    {
        StringTag? idTag = tag.Get<StringTag>("id");
        if (idTag is null || string.IsNullOrWhiteSpace(idTag.Value))
        {
            return null;
        }

        int count = Math.Max(0, tag.Get<IntTag>("count")?.Value ?? 0);
        uint metadata = unchecked((uint)(tag.Get<IntTag>("meta")?.Value ?? 0));
        return Orion.Api.Items.Items.TryCreate(idTag.Value, count, metadata);
    }

    protected virtual long GetContainerEntityUniqueId()
    {
        return -1;
    }

    protected virtual bool CanOpen(IPlayer player, int windowId)
    {
        return true;
    }

    public bool RemoveViewer(IPlayer player, bool sendClosePacket)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!occupants.Remove(player, out int id))
        {
            return false;
        }

        player.UnregisterOpenContainer(id);
        OnViewerRemoved(player, id);

        if (!sendClosePacket)
        {
            return true;
        }

        ContainerClosePacket packet = new()
        {
            WindowId = (byte)id,
            ContainerType = unchecked((byte)(int)Type),
            ServerSide = true
        };
        if (player.Spawned)
        {
            player.Send(new OpaqueOutboundPacket(packet));
        }

        return true;
    }

    protected virtual BlockPos GetContainerPosition()
    {
        return new BlockPos
        {
            X = 0,
            Y = 0,
            Z = 0
        };
    }

    protected virtual byte GetFullContainerNameId()
    {
        return Type == WireContainerType.Inventory ? (byte)0x1B : (byte)7;
    }

    protected FullContainerName GetFullContainerName(int windowId)
    {
        FullContainerName name = new()
        {
            ContainerId = GetFullContainerNameId()
        };

        if (Type != WireContainerType.Inventory)
        {
            name.DynamicContainerId = (uint)windowId;
        }

        return name;
    }

    protected virtual void OnViewerAdded(IPlayer player, int windowId)
    {
    }

    protected virtual void OnViewerRemoved(IPlayer player, int windowId)
    {
    }

    protected byte GetFullContainerNhameId()
    {
        return GetFullContainerNameId();
    }

    protected static LegacyItem ToNetworkItem(IItemStack? item)
    {
        if (item is null || item.Type.NetworkId == 0 || item.Count == 0)
        {
            return new LegacyItem();
        }

        return new LegacyItem
        {
            NetworkId = item.Type.NetworkId,
            StackSize = (ushort)item.Count,
            Metadata = unchecked((int)item.Metadata),
            ItemStackId = item.NetworkStackId,
            NetworkBlockId = 0,
            ExtraData = new ItemInstanceUserData
            {
                Nbt = null,
                CanPlaceOn = [],
                CanDestroy = [],
                Ticking = null
            }
        };
    }

    protected static NetworkItemStackDescriptor ToItemInstanceNew(IItemStack? item)
    {
        if (item is null || item.Type.NetworkId == 0 || item.Count == 0)
        {
            return new NetworkItemStackDescriptor();
        }

        return new NetworkItemStackDescriptor
        {
            NetworkId = item.Type.NetworkId,
            Count = (ushort)item.Count,
            Metadata = item.Metadata,
            StackNetworkId = item.NetworkStackId,
            BlockRuntimeId = 0,
            Nbt = null,
            CanPlaceOn = [],
            CanDestroy = [],
            BlockingTick = 0
        };
    }
}
