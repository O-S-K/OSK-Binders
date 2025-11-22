# 🚀 OSK-Binders: Overview & Features

**OSK-Binders** is a lightweight, efficient **reactive-binding framework** for **Unity**. It provides a clean way to bind data between **Observable<T>** containers and **Unity UI** components, simplifying state management and UI updates.

## ✨ Core Features

* **Observable<T>:** Reactive data container that notifies listeners upon value changes. Supports both **silent** and **notify** updates.
* **Disposer:** A mechanism to manage all binding listeners, ensuring **no memory leaks** by enforcing a clean `using` pattern.
* **VirtualRecycleViewAdapter:** A high-performance scroll adapter for large list rendering, combining **virtualization** and **pooling**.
* **BindComponent (Auto Injection System):** Automatically assigns references to fields using an **attribute-based resolver**.

---

## 🔗 UI Binding Support

The framework includes built-in bindings for common Unity UI components:

| UI Component | Supported | UI Component | Supported |
| :--- | :---: | :--- | :---: |
| TMP\_Text | ✔ | Toggle | ✔ |
| UnityEngine.UI.Text | ✔ | Button | ✔ |
| Image | ✔ | TMP\_Dropdown | ✔ |
| SpriteRenderer | ✔ | InputField / TMP\_InputField | ✔ |
| Slider (one/two-way) | ✔ | CanvasGroup | ✔ |
| Animator | ✔ | GameObject.SetActive | ✔ |

---

## 🛠️ BindComponent Details (Auto Injection)

The **BindComponent** system uses attributes to automatically find and assign references:

### 🔍 Supported `BindFrom` (Binding Source)

| Value | Description |
| :--- | :--- |
| `Self` | `GetComponent<T>()` |
| `Children` | `GetComponentInChildren<T>()` |
| `Parent` | `GetComponentInParent<T>()` |
| `Scene` | Find in active scene |
| `Resources` | `Resources.Load<T>(path)` |
| `StaticMethod` | Call static method: `Type.Method() -> object` |
| `Method` | Call instance method: `this.Method() -> object` |

### 🎯 Supported `FindBy` (Search Method)

| Value | Description |
| :--- | :--- |
| `Tag` | `GameObject.FindWithTag()` |
| `Type` | `FindObjectOfType<T>()` |
| `Name` | `GameObject.Find("Name")` |

---

## 📈 VirtualRecycleViewAdapter

An optimized scroll adapter with features including:

* **Virtualization + Pooling:** Handles thousands of items without lag.
* **Orientation:** Supports Vertical & Horizontal scrolling.
* **Data Sources:** Supports `ObservableCollection`, `List<T>`, `T[]`, `Dictionary<TKey,TValue>`.
* **Functionality:** Lazy loading, **JumpTo** index / start / center / end, and **DOTween easing**.

---

## 💻 Installation and Basic Usage

### 📥 Installation

* **Git URL:** `https://github.com/O-S-K/OSK-Binders.git`
* **Option A:** Copy the `OSK-Binders` folder into your Unity project.
* **Option B:** Use the **Unity Package Manager (UPM)** via **Add package from Git URL**.

### 📝 Usage Examples

1.  **Binding UI to Observable<T>:**
    ```csharp
    public Observable<int> Score = new Observable<int>(0);

    void Start() {
        // Binds Score to scoreText, formatting it to "Score: {v}"
        scoreText.Bind(Score, v => $"Score: {v}");
    }
    ```

2.  **Slider Two-Way Binding:**
    ```csharp
    // Updates Slider when Health changes AND updates Health when Slider changes
    healthSlider.Bind(Health, MaxHealth, twoWay: true); 
    ```

3.  **Disposable Pattern (Lifecycle Management):**
    ```csharp
    IDisposable bindDisp;

    void OnEnable() {
        bindDisp = scoreText.Bind(Score); // Create the binding
    }

    void OnDisable() {
        bindDisp?.Dispose(); // Dispose to prevent leaks
    }
    ```

4.  **Updating Observable<T>:**
    ```csharp
    Score.Value = 20;           // Updates AND notifies (reactive)
    Score.SetValue(20, false);  // Updates silently (non-reactive)
    ```

---

## ✉️ Support

* **Email:** `gamecoding1999@gmail.com`
* **Facebook:** `https://www.facebook.com/xOskx/`