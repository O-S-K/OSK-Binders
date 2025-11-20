using UnityEngine;

namespace OSK
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
