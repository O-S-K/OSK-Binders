using UnityEngine;

namespace OSK.Bindings
{

    public class ObservableRangeAttribute : PropertyAttribute
    {
        public float Min;
        public float Max;

        public ObservableRangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }

}
