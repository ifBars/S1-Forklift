using HarmonyLib;
using UnityEngine;

#if IL2CPP
using S1Avatar = Il2CppScheduleOne.AvatarFramework.Avatar;
using S1AvatarAnimation = Il2CppScheduleOne.AvatarFramework.Animation.AvatarAnimation;
using S1AvatarIKController = Il2CppScheduleOne.AvatarFramework.Animation.AvatarIKController;
#else
using S1Avatar = ScheduleOne.AvatarFramework.Avatar;
using S1AvatarAnimation = ScheduleOne.AvatarFramework.Animation.AvatarAnimation;
using S1AvatarIKController = ScheduleOne.AvatarFramework.Animation.AvatarIKController;
#endif

namespace S1Forklift.Runtime;

internal static class AvatarAccess
{
#if !IL2CPP
    // 0.4.7 keeps these serialized references private in the Mono assembly.
    private static readonly System.Reflection.FieldInfo AnimatorField =
        AccessTools.Field(typeof(S1AvatarAnimation), "_animator");
    private static readonly System.Reflection.FieldInfo IKControllerField =
        AccessTools.Field(typeof(S1AvatarAnimation), "_ikController");
    private static readonly System.Reflection.FieldInfo BodyContainerField =
        AccessTools.Field(typeof(S1Avatar), "BodyContainer");
#endif

    internal static Animator? GetAnimator(S1AvatarAnimation? animation)
    {
        if (animation == null)
            return null;
#if IL2CPP
        return animation._animator;
#else
        return (Animator?)AnimatorField.GetValue(animation);
#endif
    }

    internal static S1AvatarIKController? GetIKController(S1AvatarAnimation? animation)
    {
        if (animation == null)
            return null;
#if IL2CPP
        return animation._ikController;
#else
        return (S1AvatarIKController?)IKControllerField.GetValue(animation);
#endif
    }

    internal static Transform GetBodyContainer(S1Avatar avatar)
    {
#if IL2CPP
        return avatar.BodyContainer;
#else
        return (Transform)BodyContainerField.GetValue(avatar);
#endif
    }
}
