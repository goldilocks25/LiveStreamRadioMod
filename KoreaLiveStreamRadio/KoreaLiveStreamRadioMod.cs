using Colossal.Logging;
using Game;
using Game.Modding;
using HarmonyLib;
using KoreaLiveStreamRadio.Mono;

namespace KoreaLiveStreamRadio
{
    public class KoreaLiveStreamRadioMod : IMod
    {
        public static readonly ILog LOG = LogManager.GetLogger($"{nameof(KoreaLiveStreamRadioMod)}")
            .SetShowsErrorsInUI(false);

        private Harmony _harmony;
        public void OnLoad(UpdateSystem updateSystem)
        {
            _harmony = new Harmony($"{nameof(KoreaLiveStreamRadio)}.{nameof(KoreaLiveStreamRadioMod)}");
            _harmony.PatchAll(typeof(KoreaLiveStreamRadioMod).Assembly);
            LOG.Info(nameof(OnLoad));
        }

        public void OnDispose()
        {
            if (RadioController.AudioSource != null)
            {
                RadioController.AudioSource.Stop();
            }
            _harmony.UnpatchAll();
            LOG.Info(nameof(OnDispose));
        }
        
    }
}