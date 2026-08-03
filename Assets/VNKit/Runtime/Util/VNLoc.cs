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
                {"gallery.title", "CG Gallery"}, {"gallery.locked", "???"}, {"gallery.hint", "Click to close"},
                {"loading.init", "Initializing..."}, {"loading.assets", "Preloading assets..."},
                {"loading.ready", "Ready"}, {"loading.done", "Done"}, {"loading.save", "Loading save..."},
                {"minigame.lockpick.hint", "A/D or mouse — move pick, hold SPACE — turn lock"},
                {"minigame.lockpick.success", "Unlocked!"}, {"minigame.lockpick.fail", "The pick broke..."},
                {"minigame.picks", "Picks"},
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
                {"gallery.title", "CG-галерея"}, {"gallery.locked", "???"}, {"gallery.hint", "Клик — закрыть"},
                {"loading.init", "Инициализация..."}, {"loading.assets", "Предзагрузка ресурсов..."},
                {"loading.ready", "Готово"}, {"loading.done", "Готово"}, {"loading.save", "Загрузка сохранения..."},
                {"minigame.lockpick.hint", "A/D или мышь — двигать отмычку, держите ПРОБЕЛ — крутить замок"},
                {"minigame.lockpick.success", "Открыто!"}, {"minigame.lockpick.fail", "Отмычка сломалась..."},
                {"minigame.picks", "Отмычки"},
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
