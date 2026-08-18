using System.Collections.Generic;

namespace VNKit
{
    /// <summary>
    /// Tiny built-in localization for engine UI strings (Naninovel-style).
    /// Ships with English and Russian; add more languages at runtime:
    ///   VNLoc.Add("ja", new Dictionary&lt;string,string&gt; { {"title.newgame","はじめから"}, ... });
    /// The active language follows VNSettings.language. Script text localization works
    /// through script variants: register "Demo.ru" (Demo.ru.vns) next to "Demo" —
    /// the engine picks the variant matching the current language automatically.
    /// </summary>
    public static class VNLoc
    {
        public static string Language = "en";

        /// <summary>2.12.3: fired after Language changes via SetLanguage — bound UI
        /// labels (VNLocLabel) re-translate themselves immediately, so switching the
        /// language in settings no longer requires a game restart.</summary>
        public static event System.Action LanguageChanged;

        /// <summary>Switch the active language and notify bound UI. Direct field
        /// assignment (VNLoc.Language = ...) still works but fires no notification.</summary>
        public static void SetLanguage(string lang)
        {
            if (string.IsNullOrEmpty(lang) || lang == Language) return;
            Language = lang;
            if (LanguageChanged != null) LanguageChanged();
        }

        static readonly Dictionary<string, Dictionary<string, string>> tables =
            new Dictionary<string, Dictionary<string, string>>();

        static VNLoc()
        {
            var en = new Dictionary<string, string>
            {
                {"title.newgame", "New Game"}, {"title.load", "Load"}, {"title.settings", "Settings"},
                {"title.gallery", "Gallery"}, {"title.quit", "Quit"}, {"title.footer", "Powered by VNKit"},
                {"qm.backlog", "Backlog"}, {"qm.save", "Save"}, {"qm.load", "Load"},
                {"qm.auto", "Auto"}, {"qm.skip", "Skip"}, {"qm.settings", "Settings"},
                {"qm.title", "Title"}, {"qm.gallery", "CG"},
                {"backlog.title", "Backlog"}, {"backlog.empty", "Nothing yet."},
                {"saveload.save", "Save Game"}, {"saveload.load", "Load Game"},
                {"saveload.slot", "Slot "}, {"saveload.empty", "- empty -"},
                {"settings.title", "Settings"}, {"settings.sound", "Sound"}, {"settings.video", "Video"}, {"settings.game", "Game"},
                {"settings.master", "Master Volume"}, {"settings.bgm", "BGM Volume"},
                {"settings.sfx", "SFX Volume"}, {"settings.voice", "Voice Volume"},
                {"settings.resolution", "Resolution"}, {"settings.fullscreen", "Fullscreen"},
                {"settings.reshint", "Resolution applies immediately."},
                {"settings.textspeed", "Text Speed"}, {"settings.autospeed", "Auto Play Speed"},
                {"settings.skipunread", "Skip only already-seen text"},
                {"settings.language", "Language"}, {"settings.controls", "— Controls —"},
                {"settings.hk.advance", "Advance"}, {"settings.hk.hide", "Hide UI"}, {"settings.hk.cancel", "Cancel / Close"},
                {"settings.hk.rollback", "Rollback"}, {"settings.skipkey", "Skip (hold)"}, {"settings.autokey", "Auto toggle"},
                {"settings.presskey", "Press any key..."},
                {"gallery.title", "CG Gallery"}, {"gallery.locked", "???"}, {"gallery.hint", "Click: zoom · wheel: zoom · drag: pan · right click / Esc: close"},
                {"loading.init", "Initializing..."}, {"loading.assets", "Preloading assets..."},
                {"loading.ready", "Ready"}, {"loading.done", "Done"}, {"loading.save", "Loading save..."},
                {"minigame.lockpick.hint", "A/D or mouse — move pick, hold SPACE — turn lock"},
                {"minigame.lockpick.success", "Unlocked!"}, {"minigame.lockpick.fail", "The pick broke..."},
                {"minigame.picks", "Picks"},
                {"input.prompt", "Enter your name"}, {"input.confirm", "Sign"},
                {"input.defaultName", "Alex"}, {"input.hint", "Type here..."},
                {"phone.online", "online"}, {"phone.typing", "typing"},
                {"phone.home", "Phone"}, {"phone.chats", "Chats"}, {"phone.photos", "Photos"},
                {"phone.photo", "[photo]"}, {"phone.nochats", "No chats yet."}, {"phone.nophotos", "No photos yet."}, {"phone.unread", "New messages"},
                {"phone.app.chats", "Chats"}, {"phone.app.photos", "Photos"}, {"phone.app.backlog", "Backlog"},
                {"phone.app.save", "Save"}, {"phone.app.load", "Load"},
                {"phone.app.settings", "Settings"}, {"phone.app.title", "Main menu"},
                // 2.12 — phone gameplay apps
                {"phone.app.gallery", "Gallery"}, {"phone.app.contacts", "Contacts"},
                {"phone.app.notes", "Notes"}, {"phone.app.schedule", "Schedule"}, {"phone.app.games", "Games"},
                {"phone.gallery", "Gallery"}, {"phone.contacts", "Contacts"},
                {"phone.notes", "Notes"}, {"phone.schedule", "Schedule"}, {"phone.games", "Games"},
                {"phone.continue", "Continue"}, {"phone.nocontacts", "No contacts yet."},
                {"phone.nonotes", "No notes yet."}, {"phone.noschedule", "Nothing scheduled."},
                {"phone.nogames", "No games installed."}, {"phone.game.plays", "Played:"},
                {"phone.affection", "Affection"}, {"phone.trust", "Trust"},
                {"phone.reliability", "Reliability"},
                {"phone.openchat", "Open chat"}, {"phone.actions", "Actions"},
                {"phone.prefs", "Game settings"}, {"phone.locked", "Locked"},
                {"phone.viewed", "Viewed"},
                {"note.cat.general", "General"}, {"note.cat.people", "People"},
                {"note.cat.places", "Places"}, {"note.cat.events", "Events"},
                {"note.cat.evidence", "Evidence"}, {"note.cat.secrets", "Secrets"},
                {"debug.title", "Debug (F8)"}, {"debug.phone", "Phone data"},
                {"debug.counts", "notes: {notes} · events: {events} · gallery: {gallery} · actions: {actions}"},
                {"debug.vars", "Variables"}, {"debug.addnote", "+ test note"}, {"debug.addevent", "+ test event"},
                {"menu.title", "Menu"}, {"menu.resume", "Continue"},
                {"menu.save", "Save"}, {"menu.load", "Load"},
            };
            var ru = new Dictionary<string, string>
            {
                {"title.newgame", "Новая игра"}, {"title.load", "Загрузить"}, {"title.settings", "Настройки"},
                {"title.gallery", "Галерея"}, {"title.quit", "Выход"}, {"title.footer", "Сделано на VNKit"},
                {"qm.backlog", "История"}, {"qm.save", "Сохр."}, {"qm.load", "Загр."},
                {"qm.auto", "Авто"}, {"qm.skip", "Пропуск"}, {"qm.settings", "Настройки"},
                {"qm.title", "Меню"}, {"qm.gallery", "CG"},
                {"backlog.title", "История"}, {"backlog.empty", "Пока пусто."},
                {"saveload.save", "Сохранить игру"}, {"saveload.load", "Загрузить игру"},
                {"saveload.slot", "Слот "}, {"saveload.empty", "- пусто -"},
                {"settings.title", "Настройки"}, {"settings.sound", "Звук"}, {"settings.video", "Видео"}, {"settings.game", "Игра"},
                {"settings.master", "Общая громкость"}, {"settings.bgm", "Громкость музыки"},
                {"settings.sfx", "Громкость звуков"}, {"settings.voice", "Громкость голоса"},
                {"settings.resolution", "Разрешение"}, {"settings.fullscreen", "Полный экран"},
                {"settings.reshint", "Разрешение применяется сразу."},
                {"settings.textspeed", "Скорость текста"}, {"settings.autospeed", "Скорость авточтения"},
                {"settings.skipunread", "Пропускать только прочитанное"},
                {"settings.language", "Язык"}, {"settings.controls", "— Управление —"},
                {"settings.hk.advance", "Далее"}, {"settings.hk.hide", "Скрыть UI"}, {"settings.hk.cancel", "Отмена / Закрыть"},
                {"settings.hk.rollback", "Откат"}, {"settings.skipkey", "Пропуск (держать)"}, {"settings.autokey", "Авточтение"},
                {"settings.presskey", "Нажмите любую клавишу..."},
                {"gallery.title", "CG-галерея"}, {"gallery.locked", "???"}, {"gallery.hint", "Клик: приблизить · колесо: масштаб · перетаскивание: сдвиг · правый клик / Esc: закрыть"},
                {"loading.init", "Инициализация..."}, {"loading.assets", "Предзагрузка ресурсов..."},
                {"loading.ready", "Готово"}, {"loading.done", "Готово"}, {"loading.save", "Загрузка сохранения..."},
                {"minigame.lockpick.hint", "A/D или мышь — двигать отмычку, держите ПРОБЕЛ — крутить замок"},
                {"minigame.lockpick.success", "Открыто!"}, {"minigame.lockpick.fail", "Отмычка сломалась..."},
                {"minigame.picks", "Отмычки"},
                {"input.prompt", "Введите имя"}, {"input.confirm", "Подписать"},
                {"input.defaultName", "Алекс"}, {"input.hint", "Пишите здесь..."},
                {"phone.online", "в сети"}, {"phone.typing", "печатает"},
                {"phone.home", "Телефон"}, {"phone.chats", "Чаты"}, {"phone.photos", "Фото"},
                {"phone.photo", "[фото]"}, {"phone.nochats", "Пока нет чатов."}, {"phone.nophotos", "Пока нет фото."}, {"phone.unread", "Новые сообщения"},
                {"phone.app.chats", "Чаты"}, {"phone.app.photos", "Фото"}, {"phone.app.backlog", "История"},
                {"phone.app.save", "Сохранить"}, {"phone.app.load", "Загрузить"},
                {"phone.app.settings", "Настройки"}, {"phone.app.title", "В меню"},
                // 2.12 — игровые приложения телефона
                {"phone.app.gallery", "Галерея"}, {"phone.app.contacts", "Контакты"},
                {"phone.app.notes", "Заметки"}, {"phone.app.schedule", "Расписание"}, {"phone.app.games", "Игры"},
                {"phone.gallery", "Галерея"}, {"phone.contacts", "Контакты"},
                {"phone.notes", "Заметки"}, {"phone.schedule", "Расписание"}, {"phone.games", "Игры"},
                {"phone.continue", "Далее"}, {"phone.nocontacts", "Пока нет контактов."},
                {"phone.nonotes", "Пока нет заметок."}, {"phone.noschedule", "В расписании пусто."},
                {"phone.nogames", "Нет установленных игр."}, {"phone.game.plays", "Сыграно:"},
                {"phone.affection", "Симпатия"}, {"phone.trust", "Доверие"},
                {"phone.reliability", "Надёжность"},
                {"phone.openchat", "Открыть чат"}, {"phone.actions", "Действия"},
                {"phone.prefs", "Настройки игры"}, {"phone.locked", "Заблокировано"},
                {"phone.viewed", "Просмотрено"},
                {"note.cat.general", "Общее"}, {"note.cat.people", "Люди"},
                {"note.cat.places", "Места"}, {"note.cat.events", "События"},
                {"note.cat.evidence", "Улики"}, {"note.cat.secrets", "Секреты"},
                {"debug.title", "Отладка (F8)"}, {"debug.phone", "Данные телефона"},
                {"debug.counts", "заметок: {notes} · событий: {events} · галерея: {gallery} · действий: {actions}"},
                {"debug.vars", "Переменные"}, {"debug.addnote", "+ тестовая заметка"}, {"debug.addevent", "+ тестовое событие"},
                {"menu.title", "Меню"}, {"menu.resume", "Продолжить"},
                {"menu.save", "Сохранить"}, {"menu.load", "Загрузить"},
            };
            tables["en"] = en;
            tables["ru"] = ru;
        }

        /// <summary>Register or extend a language table.</summary>
        public static void Add(string language, Dictionary<string, string> entries)
        {
            if (string.IsNullOrEmpty(language) || entries == null) return;
            Dictionary<string, string> table;
            if (!tables.TryGetValue(language, out table))
            {
                table = new Dictionary<string, string>();
                tables[language] = table;
            }
            foreach (var kv in entries) table[kv.Key] = kv.Value;
        }

        /// <summary>Translate a key; falls back to English, then to the key itself.</summary>
        public static string T(string key)
        {
            Dictionary<string, string> table;
            string v;
            if (tables.TryGetValue(Language, out table) && table.TryGetValue(key, out v)) return v;
            if (tables.TryGetValue("en", out table) && table.TryGetValue(key, out v)) return v;
            return key;
        }
    }
}
