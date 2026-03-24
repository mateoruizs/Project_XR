Shader "UI/StencilWriter_NoColor"
{
    Properties
    {
        _StencilRef ("Stencil Ref", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off

        // No dibuja nada (solo stencil)
        ColorMask 0

        Pass
        {
            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }
        }
    }
}
