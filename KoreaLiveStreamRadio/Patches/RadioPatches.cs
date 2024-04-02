using KoreaLiveStreamRadio.Code;
using Game.Audio.Radio;
using HarmonyLib;

namespace KoreaLiveStreamRadio.Patches;


[HarmonyPatch(typeof( Radio ), "LoadRadio")]
class RealtimeRadio
{
    public static Radio Radio;
    public static Traverse Traverse;
    
    static void Postfix(Radio __instance) {
        Radio = __instance;
        Traverse = Traverse.Create(__instance);
        KoreaRadioBroadcasting.CreateBroadCast();
    }
}