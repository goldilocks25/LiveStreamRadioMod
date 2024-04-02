using Colossal.Logging;
using Game;
using Game.Modding;
using HarmonyLib;

namespace KoreaLiveStreamRadio
{
    public class KoreaLiveStreamRadioMod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(KoreaLiveStreamRadioMod)}")
            .SetShowsErrorsInUI(false);

        private Harmony harmony;

        public void OnLoad(UpdateSystem updateSystem)
        {
            harmony = new Harmony($"{nameof(KoreaLiveStreamRadio)}.{nameof(KoreaLiveStreamRadioMod)}");
            harmony.PatchAll(typeof(KoreaLiveStreamRadioMod).Assembly);
            log.Info(nameof(OnLoad));
        }

        public void OnDispose()
        {
            harmony.UnpatchAll();
            log.Info(nameof(OnDispose));
        }
        
    }
}