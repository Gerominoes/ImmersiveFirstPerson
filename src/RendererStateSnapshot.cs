using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveFirstPerson;

internal readonly struct RendererStateSnapshot
{
    private readonly bool _enabled;
    private readonly ShadowCastingMode _shadowCastingMode;
    private readonly bool _receiveShadows;
    private readonly bool _forceRenderingOff;
    private readonly int _layer;
    private readonly Material[] _sharedMaterials;

    internal RendererStateSnapshot(Renderer renderer)
    {
        // The snapshot captures every renderer property this mod may alter.
        _enabled = renderer.enabled;
        _shadowCastingMode = renderer.shadowCastingMode;
        _receiveShadows = renderer.receiveShadows;
        _forceRenderingOff = renderer.forceRenderingOff;
        _layer = renderer.gameObject.layer;
        _sharedMaterials = renderer.sharedMaterials ?? Array.Empty<Material>();
    }

    internal void Restore(Renderer renderer)
    {
        if (renderer == null)
            return;

        renderer.enabled = _enabled;
        renderer.shadowCastingMode = _shadowCastingMode;
        renderer.receiveShadows = _receiveShadows;
        renderer.forceRenderingOff = _forceRenderingOff;
        renderer.sharedMaterials = _sharedMaterials;

        if (renderer.gameObject != null)
            renderer.gameObject.layer = _layer;
    }
}
