# wpfcheck — проверка кода приложения без Windows

WPF собирается только на Windows, но типы и имена можно проверить где угодно.

```
python3 tools/wpfcheck/gen.py && dotnet build tools/wpfcheck/wpfcheck.csproj -v q -nologo
```

- `gen.py` читает все XAML и делает заглушки (поля для `x:Name`, пустой `InitializeComponent`), а заодно сообщает
  про обработчики событий и ключи ресурсов, которых нет.
- `wpfcheck.csproj` компилирует весь C# приложения с этими заглушками против справочных сборок
  `Microsoft.WindowsDesktop.App.Ref` (пакет NuGet, нужен в кэше: `dotnet restore tests/PokeCorner.App.Tests -p:EnableWindowsTargeting=true`).

Это не замена сборке на Windows: разметка XAML (шаблоны, триггеры, типы свойств) и тесты приложения проверяются только там.
