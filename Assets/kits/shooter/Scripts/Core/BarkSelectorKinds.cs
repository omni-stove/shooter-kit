using System.Numerics;
using GameKit.Core;

namespace Shooter.Core
{
    // キットの BarkSelector は種類をキーの文字列で受けるので、シューターの BarkKind を BarkSpec.KeyOf のキーにして渡す。
    public static class BarkSelectorKinds
    {
        public static BarkVerdict TryBark(this BarkSelector selector, string speakerId, BarkKind kind, float time, Vector3 position)
        {
            if (selector == null)
            {
                throw new System.ArgumentNullException(nameof(selector));
            }

            return selector.TryBark(speakerId, BarkSpec.KeyOf(kind), time, position);
        }
    }
}
