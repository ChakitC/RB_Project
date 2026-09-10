//////////////////////////////////////////////////////
// MK Toon Editor Properties           			    //
//					                                //
// Created by Michael Kremmel                       //
// www.michaelkremmel.de                            //
// Copyright © 2020 All rights reserved.            //
//////////////////////////////////////////////////////

#if UNITY_EDITOR
using MK.Toon;

namespace MK.Toon.Editor
{
    internal static class EditorProperties
    {
        /////////////////
        // Editor Only //
        /////////////////
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        internal static readonly BoolProperty initialized   = new BoolProperty(Uniforms.initialized);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        internal static readonly BoolProperty optionsTab    = new BoolProperty(Uniforms.optionsTab);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        internal static readonly BoolProperty inputTab      = new BoolProperty(Uniforms.inputTab);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        internal static readonly BoolProperty stylizeTab    = new BoolProperty(Uniforms.stylizeTab);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        internal static readonly BoolProperty advancedTab   = new BoolProperty(Uniforms.advancedTab);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        internal static readonly BoolProperty particlesTab = new BoolProperty(Uniforms.particlesTab);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        internal static readonly BoolProperty outlineTab    = new BoolProperty(Uniforms.outlineTab);
        #if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
        #endif
        internal static readonly BoolProperty refractionTab = new BoolProperty(Uniforms.refractionTab);
        
    }
}
#endif