public readonly struct PreCastBlockReservation
{
    public readonly PreCastBlockController Controller;
    public readonly int RequestId;
    public readonly int ReservationId;
    public bool IsValid => Controller != null && RequestId > 0 && ReservationId > 0;

    public PreCastBlockReservation(PreCastBlockController controller, int requestId, int reservationId)
    {
        Controller = controller;
        RequestId = requestId;
        ReservationId = reservationId;
    }
}
