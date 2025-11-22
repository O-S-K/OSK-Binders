using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OSK
{
    public interface IExampleInterface
    {
        void ExampleMethod();
    }
    public class ExampleInterface :MonoBehaviour, IExampleInterface
    {
        public void ExampleMethod()
        {
            Debug.Log("ExampleMethod called");
        }
    }
}
