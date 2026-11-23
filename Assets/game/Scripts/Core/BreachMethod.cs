namespace Shooter.Core
{
    // オンラインで番号のまま送るので、並びを変えると古いクライアントと食い違う。足すときは末尾に足す。
    public enum BreachMethod
    {
        Kick,

        Charge,
    }
}
