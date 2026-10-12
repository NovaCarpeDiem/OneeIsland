# 🤝 Contributing to Onee Island

Thank you for your interest in contributing to **Onee Island**! 🎉 We welcome community contributions, whether you're fixing a bug, designing 3D avatar animations, improving UI components, adding language translations, or suggesting new productivity features.

---

## 🧭 Code of Conduct

We are committed to providing a welcoming, inclusive, and harassment-free environment for everyone. Please be respectful, constructive, and kind in all communications, issues, and pull requests.

---

## 🛠️ Getting Started

### 1. Prerequisites
- **OS:** Windows 10 (Build 19041+) or Windows 11
- **SDK:** [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- **IDE:** Visual Studio 2022 / Rider / VS Code with C# Dev Kit
- **Git**

### 2. Fork & Clone
1. Fork the repository to your GitHub account:
   [https://github.com/NovaCarpeDiem/OneeIsland/fork](https://github.com/NovaCarpeDiem/OneeIsland/fork)
2. Clone your fork locally:
   ```bash
   git clone https://github.com/<your-username>/OneeIsland.git
   cd OneeIsland
   ```
3. Create a descriptive feature branch:
   ```bash
   git checkout -b feature/awesome-new-widget
   # or
   git checkout -b fix/media-slider-glitch
   ```

### 3. Build & Run
Run the application from the root directory:
```bash
dotnet build
dotnet run
```
Or execute `Baslat.bat` to test portable execution.

---

## 💡 How Can You Contribute?

### 🐛 Reporting Bugs
- Search existing [Issues](https://github.com/NovaCarpeDiem/OneeIsland/issues) first to avoid duplicates.
- Use our [Bug Report Template](.github/ISSUE_TEMPLATE/bug_report.yml) and include:
  - Your Windows version (Win 10 / Win 11).
  - Clear steps to reproduce the bug.
  - Screenshots or screen recordings if relevant.

### ✨ Suggesting Features
- We love creative ideas for the Dynamic Island!
- Open a feature request using our [Feature Request Template](.github/ISSUE_TEMPLATE/feature_request.yml).
- Explain the user problem and how your suggested feature enhances the island or Onee companion.

### 🌐 Adding New Languages (Localization)
Adding a new language is quick and straightforward:
1. Open [`Services/LocalizationService.cs`](Services/LocalizationService.cs).
2. Add your language code to `SupportedLanguages`.
3. Add the translated strings to the dictionary.
4. Update [`SettingsWindow.xaml`](SettingsWindow.xaml) to include the language selector pill.
5. Submit a PR!

### 🎨 3D Avatar & Graphic Assets
- Onee uses Three.js rendered in a lightweight transparent WebView2 layer located in `Avatar/`.
- New emotions, idle gestures, or interactive accessories can be tested directly in `Avatar/index.html`.

---

## 📝 Pull Request Guidelines

To ensure smooth code review and merging:

1. **Follow Existing Code Style:**
   - C# naming conventions: `PascalCase` for classes and methods, `_camelCase` for private fields.
   - Keep XAML layouts modular and utilize spring animation constants defined in `MainWindow.xaml.cs`.
2. **Keep PRs Focused:**
   - One PR should address one specific feature or bug fix.
3. **Verify Locally:**
   - Ensure the solution builds cleanly without compiler errors or warnings:
     ```bash
     dotnet build -c Release
     ```
4. **Write Clear Commit Messages:**
   - E.g.: `feat: add weather forecast widget`, `fix: handle null audio session in SMTC`, `docs: improve contributing guide`.
5. **Fill out the PR Template:**
   - Fill all sections of the [Pull Request Template](.github/pull_request_template.md).

---

## ☕ Support the Project

If you find Onee Island enjoyable, consider starring the repo or buying the creators a coffee:
- ⭐ **Star on GitHub:** [NovaCarpeDiem/OneeIsland](https://github.com/NovaCarpeDiem/OneeIsland)
- ☕ **Buy Me a Coffee:** [buymeacoffee.com/NovaCarpeDiem](https://www.buymeacoffee.com/NovaCarpeDiem)
