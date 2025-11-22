using OSK;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OSK.Bindings
{
    [System.Serializable]
    public class DataPlayerr
    {
        [Header("Model")]
        public Observable<string> PlayerName = new Observable<string>("Player");
        
        [ObservableRange(0f, 100f)]
        public Observable<float> Health = new Observable<float>(75f);
    
        [ObservableRange(0f, 100f)]
        public Observable<float> MaxHealth = new Observable<float>(100f);
        public Observable<Sprite> Portrait = new Observable<Sprite>();
    }
    public class PlayerObservalExample : BaseObservableOwner
    {
        [Header("UI refs")]
        public Image PortraitImage;
        public TMP_Text PlayerNameText;
        public TMP_Text HealthText;
        public Slider HealthSlider;
        public TMP_InputField PlayerNameInput;

        [Header("Model")]
        public DataPlayerr Data = new DataPlayerr();
    
    
        // Implement abstract: register bindings into _bindContext
        protected override void SetupBindings()
        {
            _bindContext.Add(PortraitImage.Bind(Data.Portrait));
            _bindContext.Add(PlayerNameText.Bind(Data.PlayerName));
            _bindContext.Add(HealthText.Bind(Data.Health, Data.MaxHealth, (h, m) => $"{h} / {m}"));
            _bindContext.Add(HealthSlider.Bind(Data.Health, Data.MaxHealth, twoWay: true));
            _bindContext.Add(PlayerNameInput.Bind(Data.PlayerName, twoWay: true));
        }

        // Optional helper you can call from code or editor to force notify all observables on this component:
        [ContextMenu("Auto ForceNotify Observables")]
        public void AutoForceNotify() => AutoForceNotifyFields();
    }
}