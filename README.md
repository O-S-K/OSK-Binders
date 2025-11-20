# OSK-Binders

## Overview

**OSK-Binders** is a lightweight reactive-binding framework for Unity.  
It provides a clean and efficient way to bind data between **Observable<T>** and Unity UI components.

The framework includes full UI binding support and an optimized **VirtualRecycleViewAdapter** for high-performance list rendering (virtualization + pooling).

---

## Features

### 🔹 Observable<T>
- Reactive data container  
- Notifies listeners on value changes  
- Supports silent / notify updates  

### 🔹 Disposer
- Manages all binding listeners  
- Prevents memory leaks  
- Clean `using` pattern  

### 🔹 UI Binding Extensions
Built-in bindings for common Unity components:

| UI Component           | Supported |
|------------------------|-----------|
| TMP_Text               | ✔ |
| UnityEngine.UI.Text    | ✔ |
| Image                  | ✔ |
| SpriteRenderer         | ✔ |
| Slider (one/two-way)   | ✔ |
| Toggle                 | ✔ |
| Button                 | ✔ |
| TMP_Dropdown           | ✔ |
| InputField / TMP_InputField | ✔ |
| CanvasGroup            | ✔ |
| Animator               | ✔ |
| GameObject.SetActive   | ✔ |

---

### 🔹 VirtualRecycleViewAdapter
High-performance scroll adapter:

- Virtualization + pooling  
- Vertical & horizontal modes  
- Lazy loading  
- JumpTo: index / start / center / end  
- DOTween easing  
- Adjustable spacing  
- Handles thousands of items smoothly  

---

## Installation

*** Link Git: https://github.com/O-S-K/OSK-Binders.git *** 
---

## Usage

### 1. Bind UI to `Observable<T>`
```csharp
public Observable<int> Score = new Observable<int>(0);

void Start() {
    scoreText.Bind(Score, v => $"Score: {v}");
}

### 2. Slider Two-Way Binding
```csharp
healthSlider.Bind(Health, MaxHealth, twoWay: true);

### 3. VirtualRecycleViewAdapter Example
```csharp
Adapter.SetSource(players);
Adapter.JumpTo(10, JumpPosition.Center, 0.4f, Ease.OutQuad);
``` 

### Tips
```csharp
Disposable Pattern
IDisposable bindDisp;

void OnEnable() {
    bindDisp = scoreText.Bind(Score);
}

void OnDisable() {
    bindDisp?.Dispose();
}
``` 

### Updating Observable<T>
```csharp
Score.Value = 20;           // notify
Score.SetValue(20, false);  // silent update
``` 

## **📞 Support**
- **Email**: gamecoding1999@gmail.com  
- **Facebook**: [OSK Framework](https://www.facebook.com/xOskx/)
