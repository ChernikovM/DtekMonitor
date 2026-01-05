# 🔌 DTEK Monitor — Telegram-бот для моніторингу відключень світла

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED)](https://www.docker.com/)
[![Telegram Bot](https://img.shields.io/badge/Telegram-Bot-26A5E4)](https://core.telegram.org/bots)
[![Bedrock SDK](https://img.shields.io/badge/Bedrock-SDK-orange)](https://github.com/spacebar/bedrock)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**DTEK Monitor** — це Telegram-бот на .NET 9, який відстежує графіки планових відключень електроенергії від ДТЕК (Київська область) та надсилає користувачам сповіщення про зміни в розкладі.

---

## 📋 Зміст

- [Можливості](#-можливості)
- [Як це працює](#-як-це-працює)
- [Архітектура](#-архітектура)
- [Технології](#-технології)
- [Швидкий старт](#-швидкий-старт)
- [Конфігурація](#-конфігурація)
- [Management API](#-management-api)
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
- 📊 **Management API** — аналітика, статистика та масові розсилки через REST API
- 🔐 **Swagger UI** — документація API з можливістю тестування

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
                                 │
                                 ▼
                        ┌─────────────────┐
                        │  Management API │
                        │  (Swagger UI)   │
                        └─────────────────┘
```

### Цикл роботи

1. **Worker** кожні 60 секунд запускає `DtekScraper`
2. **DtekScraper** використовує Playwright (headless Chrome) для завантаження сторінки ДТЕК
3. Дані витягуються з JavaScript-змінної `DisconSchedule.fact` на сторінці
4. **Worker** порівнює нові дані з попередніми
5. При виявленні змін — **NotificationService** надсилає повідомлення всім підписникам відповідних черг
6. **Bedrock SDK** обробляє команди користувачів, логує аналітику та надає Management API

---

## 🏗 Архітектура

### Компоненти системи

```
DtekMonitor/
├── Program.cs                 # Точка входу, налаштування Bedrock SDK
├── Worker.cs                  # Головний фоновий процес моніторингу
│
├── Commands/UserCommands/     # Команди Telegram бота (Bedrock SDK)
│   ├── StartCommandHandler.cs     # /start
│   ├── SetGroupCommandHandler.cs  # /setgroup + кнопка меню
│   ├── ScheduleCommandHandler.cs  # /schedule + кнопка меню
│   ├── MyGroupCommandHandler.cs   # /mygroup + кнопка меню
│   ├── StopCommandHandler.cs      # /stop
│   └── HowToCommandHandler.cs     # /howto + кнопка меню
│
├── Middleware/
│   └── DtekCallbackMiddleware.cs  # Обробка inline-кнопок
│
├── Services/
│   ├── DtekScraper.cs         # Скрапінг сайту ДТЕК через Playwright
│   ├── NotificationService.cs  # Формування та відправка сповіщень
│   ├── ScheduleFormatter.cs    # Форматування розкладу для відображення
│   ├── ScheduleKeyboards.cs    # Inline-клавіатури
│   └── KeyboardMarkups.cs      # Reply-клавіатури бота (меню)
│
├── Configurations/            # Bedrock SDK конфігурації
│   ├── TelegramBotConfig.json     # Налаштування бота
│   ├── TelegramDatabaseConfig.json # Налаштування БД
│   └── ManagementConfig.json      # Налаштування Management API
│
├── Models/
│   ├── ScheduleModels.cs      # Моделі даних ДТЕК
│   └── Subscriber.cs          # Entity підписника
│
├── Database/
│   └── AppDbContext.cs        # EF Core контекст з Bedrock SDK
│
└── Settings/
    └── ScraperSettings.cs     # Налаштування скрапера
```

### Bedrock SDK Integration

Проект використовує **Spacebar.Bedrock SDK** для:

- 🤖 **Telegram Bot** — обробка команд та повідомлень
- 📊 **Analytics** — автоматичне логування всіх взаємодій
- 🔐 **Management API** — REST API для аналітики та broadcast
- 📖 **Swagger UI** — документація API

```csharp
// Приклад команди з підтримкою кнопок меню
public class ScheduleCommandHandler : CommandHandler<ScheduleCommandHandler>
{
    public override string CommandName => "schedule";
    public override IReadOnlyList<string> Aliases => ["📅 Розклад"]; // Кнопка меню
    
    protected override async Task<string?> ExecuteAsync(UpdateContext context)
    {
        // Логіка команди
    }
}
```

---

## 🛠 Технології

| Компонент | Технологія | Версія |
|-----------|-----------|--------|
| Framework | .NET | 9.0 |
| SDK | Spacebar.Bedrock | 0.0.0-alpha.0.7 |
| Скрапінг | Microsoft.Playwright | 1.48.0 |
| Telegram | Telegram.Bot | 22.4.4 |
| База даних | PostgreSQL + EF Core | 16 + 9.0.0 |
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
cd dtek-monitor/DtekMonitor/DtekMonitor
```

2. **Створіть файл `.env`**

```bash
cp .env.example .env
nano .env
```

Заповніть значення:

```env
TELEGRAM_BOT_TOKEN=123456789:ABCdefGHIjklMNOpqrsTUVwxyz
POSTGRES_PASSWORD=your_secure_password_here
MANAGEMENT_API_KEY=your_random_api_key_here
```

> 💡 Згенерувати API ключ: `openssl rand -hex 32`

3. **Запустіть**

```bash
docker compose up -d --build
```

4. **Перевірте логи**

```bash
docker compose logs -f dtek-app
```

Ви повинні побачити:
```
Bot started: @dtek_krem_bot
Swagger UI enabled at /api/management/swagger
Database initialized successfully
```

5. **Відкрийте Swagger UI**

```
http://localhost:5000/api/management/swagger
```

---

## ⚙️ Конфігурація

### Змінні оточення (.env)

| Змінна | Опис | Обов'язково |
|--------|------|-------------|
| `TELEGRAM_BOT_TOKEN` | Токен Telegram бота | ✅ |
| `POSTGRES_PASSWORD` | Пароль PostgreSQL | ✅ |
| `MANAGEMENT_API_KEY` | API ключ для Management API | ✅ |

### Конфігураційні файли (Configurations/)

**TelegramBotConfig.json:**
```json
{
  "TelegramBotConfig": {
    "BotToken": "",
    "BotUsername": "dtek_krem_bot",
    "DropPendingUpdates": true,
    "EnableCommandRouting": true,
    "UnknownCommandMessage": "❓ Невідома команда..."
  }
}
```

**TelegramDatabaseConfig.json:**
```json
{
  "TelegramDatabaseConfig": {
    "Host": "postgres",
    "Port": 5432,
    "Database": "dtekmonitor",
    "Username": "postgres",
    "Password": "",
    "Provider": "PostgreSQL"
  }
}
```

**ManagementConfig.json:**
```json
{
  "ManagementConfig": {
    "ApiKeys": [],
    "RoutePrefix": "api/management",
    "EnablePushNotifications": false
  }
}
```

> ⚠️ **Секрети** (BotToken, Password, ApiKeys) передаються через environment variables і **перезаписують** значення з JSON файлів.

### Налаштування скрапера (docker-compose.yml)

| Параметр | Опис | Default |
|----------|------|---------|
| `Scraper__CheckIntervalSeconds` | Інтервал перевірки (сек) | 60 |
| `Scraper__WaitTimeSeconds` | Час очікування WAF (сек) | 10 |

---

## 📊 Management API

### Endpoints

| Endpoint | Метод | Опис |
|----------|-------|------|
| `/api/management/health` | GET | Перевірка здоров'я |
| `/api/management/info` | GET | Інформація про бота |
| `/api/management/stats` | GET | Загальна статистика |
| `/api/management/stats/users` | GET | Статистика користувачів |
| `/api/management/stats/messages` | GET | Статистика повідомлень |
| `/api/management/broadcast` | POST | Масова розсилка |
| `/api/management/swagger` | GET | Swagger UI |

### Аутентифікація

Всі endpoints (крім `/health`) потребують API ключ:

```bash
curl -H "X-Api-Key: your-api-key" http://localhost:5000/api/management/stats
```

### Приклад: Масова розсилка

```bash
curl -X POST http://localhost:5000/api/management/broadcast \
  -H "X-Api-Key: your-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "📢 Важливе повідомлення для всіх користувачів!",
    "parseMode": "Html"
  }'
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
│   ├── Configurations/       # Bedrock SDK конфіги
│   ├── Database/             # EF Core контекст
│   ├── Middleware/           # Telegram middleware
│   ├── Models/               # Моделі даних
│   ├── Services/             # Бізнес-логіка
│   ├── Settings/             # Класи конфігурації
│   ├── Program.cs            # Точка входу
│   ├── Worker.cs             # Background worker
│   ├── Dockerfile            # Docker образ
│   ├── docker-compose.yml    # Docker Compose
│   └── .env.example          # Приклад змінних оточення
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
cp .env.example .env
nano .env

# Заповніть:
# TELEGRAM_BOT_TOKEN=your_bot_token
# POSTGRES_PASSWORD=your_db_password
# MANAGEMENT_API_KEY=your_api_key

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

# Перевірка API
curl http://localhost:5000/api/management/health
```

### 5. Автозапуск після перезавантаження

Docker Compose з `restart: unless-stopped` автоматично перезапустить контейнери.

```bash
sudo systemctl enable docker
```

### Оновлення бота

```bash
cd ~/dtek-monitor/DtekMonitor/DtekMonitor

# 1. Створіть бекап БД (ВАЖЛИВО!)
docker exec dtek-postgres pg_dump -U postgres dtekmonitor > backup_$(date +%Y%m%d_%H%M%S).sql

# 2. Оновлення
git pull
docker compose down
docker compose up -d --build

# 3. Перевірка
docker compose logs -f --tail=50 dtek-app
```

### Відновлення з бекапу

```bash
# Якщо щось пішло не так:
docker compose down
docker exec -i dtek-postgres psql -U postgres -d dtekmonitor < backup_YYYYMMDD_HHMMSS.sql
docker compose up -d
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
                "3": "first",     // 02:00-03:00 — частково
                ...
            }
        }
    },
    "update": "25.11.2025 19:45",
    "today": 1732489200
};
```

### Формат черг

| Відображення | API формат | Опис |
|--------------|------------|------|
| `1.1` | `GPV1.1` | Черга 1, підгрупа 1 |
| `3.2` | `GPV3.2` | Черга 3, підгрупа 2 |
| `6.2` | `GPV6.2` | Черга 6, підгрупа 2 |

### Статуси електропостачання

| Статус | Значення | Emoji |
|--------|----------|-------|
| `yes` | Світло є | ✅ |
| `no` | Світла немає | 🔴 |
| `first` | Частково (перша половина години) | ⚠️½ |
| `second` | Частково (друга половина години) | ½⚠️ |

### База даних

**Таблиці:**

| Таблиця | Опис |
|---------|------|
| `Subscribers` | Підписки користувачів (бізнес-дані) |
| `TelegramUsers` | Користувачі бота (Bedrock SDK) |
| `MessageLogs` | Логи повідомлень (Bedrock SDK) |
| `CallbackLogs` | Логи callback queries (Bedrock SDK) |

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

### Як отримати доступ до Management API?

1. Згенеруйте API ключ: `openssl rand -hex 32`
2. Додайте його в `.env` як `MANAGEMENT_API_KEY`
3. Використовуйте header `X-Api-Key` в запитах
4. Swagger UI: `http://your-server:5000/api/management/swagger`

### Як зробити масову розсилку?

```bash
curl -X POST http://localhost:5000/api/management/broadcast \
  -H "X-Api-Key: your-api-key" \
  -H "Content-Type: application/json" \
  -d '{"message": "Ваше повідомлення", "parseMode": "Html"}'
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
