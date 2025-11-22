using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OSK.Bindings
{
    public class PlayerObservableExample : MonoBehaviour 
    {
        public Observable<bool> IsPanelVisible = new Observable<bool>(true);
        public Observable<string> DisplayText = new Observable<string>("Nội dung ban đầu");
    }
}
