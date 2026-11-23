namespace Shooter.Core
{
    // オンラインで番号のまま送るので、並びを変えると古いクライアントと食い違う。足すときは末尾に足す。
    public enum BreachDoorPhase
    {
        Closed,

        Planting,

        Armed,

        Opened,

        Destroyed,
    }
}
