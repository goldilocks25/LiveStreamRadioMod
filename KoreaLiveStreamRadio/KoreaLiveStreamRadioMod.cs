using Colossal.Logging;
using Game;
using Game.Modding;
using HarmonyLib;
using KoreaLiveStreamRadio.Mono;

namespace KoreaLiveStreamRadio
{
    public class KoreaLiveStreamRadioMod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(KoreaLiveStreamRadioMod)}")
            .SetShowsErrorsInUI(false);

        private Harmony _harmony;

        public void OnLoad(UpdateSystem updateSystem)
        {
            _harmony = new Harmony($"{nameof(KoreaLiveStreamRadio)}.{nameof(KoreaLiveStreamRadioMod)}");
            _harmony.PatchAll(typeof(KoreaLiveStreamRadioMod).Assembly);
            log.Info(nameof(OnLoad));
        }

        public void OnDispose()
        {
            RadioController.AudioSource.Stop();
            _harmony.UnpatchAll();
            log.Info(nameof(OnDispose));
        }
        
    }
}