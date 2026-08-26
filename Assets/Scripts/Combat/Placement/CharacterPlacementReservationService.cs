using UnityEngine;

public sealed class CharacterPlacementReservationService
{
    const int DefaultCapacity = 64;

    readonly ReservationEntry[] entries;
    int nextReservationId;
    int activeCount;

    public CharacterPlacementReservationService(int capacity = DefaultCapacity)
    {
        entries = new ReservationEntry[Mathf.Max(1, capacity)];
    }

    public int ActiveCount
    {
        get
        {
            PruneDestroyedOwners();
            return activeCount;
        }
    }

    public readonly struct StaticReservation
    {
        public StaticReservation(
            CharacterPlacementFootprint footprint,
            Vector3 position,
            Quaternion rotation,
            Object owner = null)
        {
            Footprint = footprint;
            Position = position;
            Rotation = rotation == default ? Quaternion.identity : rotation;
            Owner = owner;
        }

        public CharacterPlacementFootprint Footprint { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Object Owner { get; }
    }

    public readonly struct Handle
    {
        internal Handle(int slot, int id)
        {
            Slot = slot;
            Id = id;
        }

        internal int Slot { get; }
        internal int Id { get; }
        public bool IsValid => Id > 0;
    }

    internal readonly struct ReservationView
    {
        public ReservationView(
            CharacterPlacementRequest request,
            CharacterPlacementResult result,
            Object owner)
        {
            Request = request;
            Result = result;
            Owner = owner;
        }

        public CharacterPlacementRequest Request { get; }
        public CharacterPlacementResult Result { get; }
        public Object Owner { get; }
    }

    public bool TryReserve(
        CharacterPlacementRequest request,
        CharacterPlacementResult result,
        out Handle handle)
    {
        handle = default;
        if (request == null || !result.IsValid)
            return false;

        PruneDestroyedOwners();

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].Active)
                continue;

            int id = ++nextReservationId;
            if (id <= 0)
            {
                nextReservationId = 1;
                id = nextReservationId;
            }

            entries[i] = new ReservationEntry(
                true,
                id,
                request,
                result,
                request.ReservationOwner);
            activeCount++;
            handle = new Handle(i, id);
            return true;
        }

        return false;
    }

    public bool Release(Handle handle)
    {
        if (!TryGetEntry(handle, out ReservationEntry entry) || !entry.Active)
            return false;

        entries[handle.Slot] = default;
        activeCount--;
        return true;
    }

    public int ReleaseOwner(Object owner)
    {
        PruneDestroyedOwners();
        int released = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            if (!entries[i].Active || entries[i].Owner != owner)
                continue;

            entries[i] = default;
            activeCount--;
            released++;
        }

        return released;
    }

    public void Clear()
    {
        for (int i = 0; i < entries.Length; i++)
            entries[i] = default;

        activeCount = 0;
        nextReservationId = 0;
    }

    internal bool TryGetActiveAt(int activeIndex, out ReservationView view)
    {
        view = default;
        if (activeIndex < 0)
            return false;

        PruneDestroyedOwners();

        int current = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            ReservationEntry entry = entries[i];
            if (!entry.Active)
                continue;
            if (current++ != activeIndex)
                continue;

            view = new ReservationView(entry.Request, entry.Result, entry.Owner);
            return true;
        }

        return false;
    }

    void PruneDestroyedOwners()
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (!entries[i].Active)
                continue;

            Object owner = entries[i].Owner;
            if (owner != null || ReferenceEquals(owner, null))
                continue;

            entries[i] = default;
            activeCount--;
        }
    }

    bool TryGetEntry(Handle handle, out ReservationEntry entry)
    {
        entry = default;
        if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= entries.Length)
            return false;

        entry = entries[handle.Slot];
        return entry.Active && entry.Id == handle.Id;
    }

    readonly struct ReservationEntry
    {
        public ReservationEntry(
            bool active,
            int id,
            CharacterPlacementRequest request,
            CharacterPlacementResult result,
            Object owner)
        {
            Active = active;
            Id = id;
            Request = request;
            Result = result;
            Owner = owner;
        }

        public bool Active { get; }
        public int Id { get; }
        public CharacterPlacementRequest Request { get; }
        public CharacterPlacementResult Result { get; }
        public Object Owner { get; }
    }
}
