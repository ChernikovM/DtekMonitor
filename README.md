# 🔌 DTEK Monitor — Telegram-бот для моніторингу відключень світла

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED)](https://www.docker.com/)
[![Telegram Bot](https://img.shields.io/badge/Telegram-Bot-26A5E4)](https://core.telegram.org/bots)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**DTEK Monitor** — це Telegram-бот на .NET 8, який відстежує графіки планових відключень електроенергії від ДТЕК (Київська область) та надсилає користувачам сповіщення про зміни в розкладі.

---

## 📋 Зміст

- [Можливості](#-можливості)
- [Як це працює](#-як-це-працює)
- [Архітектура](#-архітектура)
- [Технології](#-технології)
- [Швидкий старт](#-швидкий-старт)
- [Конфігурація](#-конфігурація)
- [Команди бота](#-команди-бота)
- [Структура проекту](#-структура-проекту)
- [Розгортання на VPS](#-розгортання-на-vps)
- [Технічні деталі](#-технічні-деталі)
- [FAQ](#-faq)

---

## ✨ Можливості

- 📅 **Перегляд розкладу** на сьогодні та завтра
- 🔔 **Автоматичні сповіщення** при зміні графіка відключень
- 📆 **Сповіщення про появу розкладу на завтра** — бот повідомить, коли ДТЕК опублікує графік на наступний день
- 👥 **Підписка на чергу** — оберіть свою чергу (1.1 - 6.2) і отримуйте персоналізовані сповіщення
- 🎯 **Зручне меню** — кнопки внизу чата для швидкого доступу до функцій
- ❓ **Інструкція** — як дізнатись свою чергу на сайті ДТЕК

---

## 🔄 Як це працює

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   ДТЕК сайт     │────▶│   DtekMonitor    │────▶│  Telegram Bot   │
│  (shutdowns)    │     │   (Worker)       │     │   (Users)       │
└─────────────────┘     └──────────────────┘     └─────────────────┘
         │                       │                       │
         │                       │                       │
    Playwright              PostgreSQL              Сповіщення
    (скрапінг)              (підписки)              про зміни
```

### Цикл роботи

1. **Worker** кожні 60 секунд запускає `DtekScraper`
2. **DtekScraper** використовує Playwright (headless Chrome) для завантаження сторінки ДТЕК
3. Дані витягуються з JavaScript-змінної `DisconSchedule.fact` на сторінці
4. **Worker** порівнює нові дані з попередніми
5. При виявленні змін — **NotificationService** надсилає повідомлення всім підписникам відповідних черг
6. **BotService** обробляє команди користувачів та кнопки меню

---

## 🏗 Архітектура

### Компоненти системи

```
DtekMonitor/
├── Program.cs                 # Точка входу, налаштування DI
├── Worker.cs                  # Головний фоновий процес моніторингу
│
├── Services/
│   ├── DtekScraper.cs        # Скрапінг сайту ДТЕК через Playwright
│   ├── BotService.cs         # Telegram Bot API, обробка повідомлень
│   ├── NotificationService.cs # Формування та відправка сповіщень
│   ├── CallbackQueryHandler.cs# Обробка натискань inline-кнопок
│   ├── ScheduleFormatter.cs  # Форматування розкладу для відображення
│   └── KeyboardMarkups.cs    # Клавіатури бота (меню)
│
├── Commands/
│   ├── Abstractions/
│   │   ├── ICommandHandler.cs    # Інтерфейс команди
│   │   └── CommandHandler.cs     # Базовий клас команди
│   ├── CommandHandlerRegistry.cs # Автореєстрація команд
│   └── UserCommands/
│       ├── StartCommandHandler.cs    # /start
│       ├── SetGroupCommandHandler.cs # /setgroup
│       ├── ScheduleCommandHandler.cs # /schedule
│       ├── MyGroupCommandHandler.cs  # /mygroup
│       ├── StopCommandHandler.cs     # /stop
│       └── HowToCommandHandler.cs    # /howto
│
├── Models/
│   ├── ScheduleModels.cs     # Моделі даних ДТЕК
│   └── Subscriber.cs         # Entity підписника
│
├── Database/
│   └── AppDbContext.cs       # EF Core контекст
│
└── Settings/
    ├── TelegramSettings.cs   # Налаштування бота
    └── ScraperSettings.cs    # Налаштування скрапера
```

### Модульна архітектура команд

Команди бота реалізовані за патерном **Command Handler**:

```csharp
// Кожна команда — окремий клас
public class StartCommandHandler : CommandHandler<StartCommandHandler>
{
    public override string CommandName => "start";
    
    protected override async Task<string?> HandleCommandAsync(...)
    {
        // Логіка команди
    }
}
```

Команди автоматично реєструються при старті через рефлексію:

```csharp
CommandHandlerRegistry.RegisterAllHandlers(builder.Services);
```

---

## 🛠 Технології

| Компонент | Технологія | Версія |
|-----------|-----------|--------|
| Framework | .NET | 8.0 |
| Скрапінг | Microsoft.Playwright | 1.48.0 |
| Telegram | Telegram.Bot | 22.4.4 |
| База даних | PostgreSQL + EF Core | 16 + 8.0.10 |
| JSON | Newtonsoft.Json | 13.0.4 |
| Контейнеризація | Docker | - |

### Чому Playwright?

Сайт ДТЕК захищений **Imperva Incapsula WAF** (Web Application Firewall). Звичайні HTTP-запити через `HttpClient` блокуються. Playwright дозволяє:

- Запускати повноцінний браузер (Chromium)
- Виконувати JavaScript-челенджі WAF
- Імітувати реального користувача

---

## 🚀 Швидкий старт

### Передумови

- [Docker](https://docs.docker.com/get-docker/) та Docker Compose
- Telegram Bot Token (отримати у [@BotFather](https://t.me/BotFather))

### Кроки

1. **Клонуйте репозиторій**

```bash
git clone https://github.com/your-username/dtek-monitor.git
cd dtek-monitor/DtekMonitor
```

2. **Створіть файл `.env`**

```bash
cp .env.example .env
```

Відредагуйте `.env`:

```env
TELEGRAM__BOTTOKEN=123456789:ABCdefGHIjklMNOpqrsTUVwxyz
POSTGRES_PASSWORD=your_secure_password_here
```

3. **Запустіть**

```bash
docker-compose up -d --build
```

4. **Перевірте логи**

```bash
docker-compose logs -f dtek-app
```

Ви повинні побачити:
```
Bot started: @YourBotName
Successfully fetched schedule data
```

---

## ⚙️ Конфігурація

### Змінні оточення

| Змінна | Опис | Обов'язково |
|--------|------|-------------|
| `TELEGRAM__BOTTOKEN` | Токен Telegram бота | ✅ |
| `POSTGRES_PASSWORD` | Пароль PostgreSQL | ✅ |
| `Scraper__CheckIntervalSeconds` | Інтервал перевірки (сек) | ❌ (60) |
| `Scraper__WaitTimeSeconds` | Час очікування WAF (сек) | ❌ (10) |
| `TZ` | Часовий пояс | ❌ (Europe/Kiev) |

### appsettings.json

```json
{
  "Telegram": {
    "BotToken": ""
  },
  "Scraper": {
    "TargetUrl": "https://www.dtek-krem.com.ua/ua/shutdowns",
    "CheckIntervalSeconds": 60,
    "WaitTimeSeconds": 10,
    "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36...",
    "ViewportWidth": 1920,
    "ViewportHeight": 1080,
    "Locale": "uk-UA",
    "TimezoneId": "Europe/Kiev"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=dtekmonitor;Username=postgres;Password=..."
  }
}
```

---

## 🤖 Команди бота

### Головне меню (кнопки)

```
📅 Розклад        | 📊 Обрати групу
ℹ️ Моя група     | ❓ Як дізнатись групу
```

### Текстові команди

| Команда | Опис |
|---------|------|
| `/start` | Привітання та інструкція |
| `/setgroup` | Обрати чергу відключень |
| `/schedule` | Переглянути розклад |
| `/mygroup` | Показати поточну чергу |
| `/stop` | Відписатися від сповіщень |
| `/howto` | Як дізнатись свою чергу |

### Inline-кнопки

- **Вибір черги**: Кнопки `Черга 1.1` — `Черга 6.2`
- **Навігація по днях**: `📅 Сьогодні` / `📆 Завтра`

---

## 📁 Структура проекту

```
DtekMonitor/
├── DtekMonitor/              # Основний проект
│   ├── Commands/             # Команди Telegram бота
│   ├── Database/             # EF Core контекст
│   ├── Models/               # Моделі даних
│   ├── Services/             # Бізнес-логіка
│   ├── Settings/             # Класи конфігурації
│   ├── Program.cs            # Точка входу
│   ├── Worker.cs             # Background worker
│   ├── Dockerfile            # Docker образ
│   ├── docker-compose.yml    # Docker Compose
│   ├── appsettings.json      # Конфігурація
│   └── DtekMonitor.csproj    # Файл проекту
│
├── DtekMonitor.sln           # Solution файл
└── README.md                 # Документація
```

---

## 🖥 Розгортання на VPS

### 1. Підготовка сервера

```bash
# Оновлення системи
sudo apt update && sudo apt upgrade -y

# Встановлення Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Встановлення Docker Compose
sudo apt install docker-compose-plugin -y

# Перелогін для застосування груп
exit
# Підключіться знову
```

### 2. Клонування та налаштування

```bash
# Клонування
cd ~
git clone https://github.com/your-username/dtek-monitor.git
cd dtek-monitor/DtekMonitor/DtekMonitor

# Створення .env
cat > .env << EOF
TELEGRAM__BOTTOKEN=your_bot_token_here
POSTGRES_PASSWORD=your_secure_password_here
EOF

# Безпека
chmod 600 .env
```

### 3. Запуск

```bash
docker compose up -d --build
```

### 4. Перевірка

```bash
# Статус контейнерів
docker compose ps

# Логи
docker compose logs -f dtek-app
```

### 5. Автозапуск після перезавантаження

Docker Compose з `restart: unless-stopped` автоматично перезапустить контейнери.

Переконайтесь, що Docker запускається при старті системи:

```bash
sudo systemctl enable docker
```

### Корисні команди

```bash
# Перезапуск
docker compose restart dtek-app

# Оновлення після git pull
docker compose down
docker compose up -d --build

# Очистка старих образів
docker image prune -f

# Перегляд логів
docker compose logs -f --tail=100 dtek-app
```

---

## 🔧 Технічні деталі

### Обхід WAF (Incapsula)

Сайт ДТЕК захищений Imperva Incapsula. Для обходу використовується:

1. **Playwright з Chromium** замість HttpClient
2. **Імітація реального браузера**:
   ```csharp
   Args = [
       "--disable-blink-features=AutomationControlled",
       "--no-sandbox",
       "--disable-setuid-sandbox"
   ]
   ```
3. **Реалістичний User-Agent та Viewport**
4. **Очікування 10 секунд** для завершення JS-челенджу

### Витягування даних

Дані зберігаються в JavaScript-змінній на сторінці:

```javascript
DisconSchedule.fact = {
    "data": {
        "1732489200": {           // Unix timestamp дня
            "GPV3.2": {           // Черга (API формат)
                "1": "yes",       // 00:00-01:00 — світло є
                "2": "no",        // 01:00-02:00 — світла немає
                "3": "first",     // 02:00-03:00 — частково (перша половина)
                ...
            }
        }
    },
    "update": "25.11.2025 19:45",
    "today": 1732489200
};
```

Витягування через Playwright:

```csharp
// Спочатку пробуємо отримати напряму з JS контексту
var jsonFromJs = await page.EvaluateAsync<string>("JSON.stringify(DisconSchedule.fact)");

// Якщо не вийшло — парсимо HTML через Regex
var match = Regex.Match(content, @"DisconSchedule\.fact\s*=\s*(\{.*?\});", RegexOptions.Singleline);
```

### Формат черг

| Відображення | API формат | Опис |
|--------------|------------|------|
| `1.1` | `GPV1.1` | Черга 1, підгрупа 1 |
| `3.2` | `GPV3.2` | Черга 3, підгрупа 2 |
| ... | ... | ... |
| `6.2` | `GPV6.2` | Черга 6, підгрупа 2 |

### Статуси електропостачання

| Статус | Значення | Emoji |
|--------|----------|-------|
| `yes` | Світло є | ✅ |
| `no` | Світла немає | 🔴 |
| `first` | Частково (перша половина години) | ⚠️½ |
| `second` | Частково (друга половина години) | ½⚠️ |

### База даних

**Таблиця `Subscribers`:**

| Поле | Тип | Опис |
|------|-----|------|
| `ChatId` | `bigint` (PK) | Telegram Chat ID |
| `GroupName` | `varchar(10)` | Черга (GPV формат) |
| `Username` | `varchar(32)` | Telegram username |
| `CreatedAt` | `timestamp` | Дата підписки |
| `UpdatedAt` | `timestamp` | Дата оновлення |

---

## ❓ FAQ

### Як дізнатись свою чергу?

1. Перейдіть на [dtek-krem.com.ua/ua/shutdowns](https://www.dtek-krem.com.ua/ua/shutdowns)
2. Введіть свою адресу (населений пункт, вулиця, будинок)
3. Ви побачите вашу чергу, наприклад: **Черга 3.2**
4. Оберіть цю чергу в боті

### Чому бот не відповідає?

- Перевірте, чи запущені контейнери: `docker compose ps`
- Перегляньте логи: `docker compose logs -f dtek-app`
- Переконайтесь, що токен бота правильний

### Чому дані не оновлюються?

- WAF може блокувати запити. Перевірте логи на наявність `Incapsula`
- Спробуйте збільшити `Scraper__WaitTimeSeconds` до 15

### Як змінити інтервал перевірки?

В `docker-compose.yml`:

```yaml
environment:
  - Scraper__CheckIntervalSeconds=120  # 2 хвилини
```

### Як оновити бота?

```bash
cd ~/dtek-monitor/DtekMonitor/DtekMonitor
git pull
docker compose down
docker compose up -d --build
```

---

## 📄 Ліцензія

MIT License — використовуйте вільно!

---

## 🤝 Контрибуція

Pull requests вітаються! Для великих змін спочатку відкрийте issue.

---

## 📞 Підтримка

Якщо виникли питання — створіть [Issue](https://github.com/your-username/dtek-monitor/issues) на GitHub.

