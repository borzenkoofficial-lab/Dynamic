# Dynamic

Нативная Dynamic Island-подобная капсула для Windows 10/11.

> Статус: исследование завершено, реализация MVP начинается. Продукт не считается готовым до прохождения Windows QA.

## Цель MVP

- Нативная прозрачная капсула без системной рамки.
- Поверх окон, без кражи фокуса.
- Компактное и раскрытое состояния.
- Прерываемые spring-анимации.
- Управление положением и масштабом.
- Сохранение настроек между запусками.
- Системный трей и корректное завершение.

## Технологический курс

- C# / .NET 10
- WinUI 3 / Windows App SDK
- Win32 HWND interop
- Windows Composition и DWM
- Global System Media Transport Controls для совместимых медиасессий

## Этапы

1. Research — технические ограничения и архитектура.
2. Foundation — окно, прозрачность, позиционирование, состояния.
3. Motion — spring, morphing, interruptible transitions.
4. Modules — media, notifications, timers, progress.
5. Settings — persistence, tray, startup.
6. QA — DPI, monitors, focus, performance, shutdown.
7. Release — installer, README, changelog.

## Важно

Сборка, запуск и поведение нативного окна должны проверяться на Windows. Linux/macOS CI не является доказательством корректной работы HWND, DWM или WinUI.
