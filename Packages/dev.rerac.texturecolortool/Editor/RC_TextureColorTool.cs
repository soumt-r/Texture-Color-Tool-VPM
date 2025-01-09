using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class RC_TextureColorTool : EditorWindow
{
    [MenuItem("ReraC/Texture Color Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<RC_TextureColorTool>("Texture Color Tool");
        window.Show();
    }

    private Texture2D TargetTexture = null;
    private Texture2D ResultTexture = null;

    private bool Mode = false; // false = Add, true = Multiply
    private bool autoApply = true; // Default to true for immediate application
    private float Hf = 0;
    private float Sf = 0;
    private float Vf = 0;

    private bool isProcessing = false; // Flag to check if processing is ongoing

    private void OnEnable()
    {
        // Initialize previous values
        Hf = 0;
        Sf = 0;
        Vf = 0;
        // ComputeShader를 Assets/ReraC/Editor 폴더에서 직접 로드합니다.
        
    }

    private Texture2D HSVFilter(Texture2D target, float hueF, float saturationF, float valueF, bool mode)
    {
        int width = target.width;
        int height = target.height;

        // Create a RenderTexture to offload work to the GPU
        RenderTexture renderTexture = new RenderTexture(width, height, 0);
        Graphics.Blit(target, renderTexture);

        RenderTexture.active = renderTexture;
        Texture2D result = new Texture2D(width, height);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        RenderTexture.active = null;

        Color[] pixels = result.GetPixels();
        System.Threading.Tasks.Parallel.For(0, pixels.Length, i =>
        {
            float h, s, v;
            Color.RGBToHSV(pixels[i], out h, out s, out v);

            // 알파 값 따로 저장
            float alpha = pixels[i].a;

            if (mode)
            {
                h *= hueF;
                s *= saturationF;
                v *= valueF;
            }
            else
            {
                h += hueF;
                s += saturationF;
                v += valueF;
            }

            h = (h + 1) % 1;  // h 값이 0과 1 사이에 있도록 보정

            s = Mathf.Clamp01(s);
            v = Mathf.Clamp01(v);

            pixels[i] = Color.HSVToRGB(h, s, v);
            pixels[i].a = alpha;  // 알파 값을 다시 설정
        });

        result.SetPixels(pixels);
        result.Apply();

        return result;
    }

    /*public ComputeShader hsvShader;

    private Texture2D HSVFilter(Texture2D target, float hueF, float saturationF, float valueF, bool mode)
    {
        int width = target.width;
        int height = target.height;

        RenderTexture result = new RenderTexture(width, height, 0);
        result.enableRandomWrite = true;
        result.Create();

        // ComputeShader 설정
        int kernelHandle = hsvShader.FindKernel("HSVFilter");
        hsvShader.SetTexture(kernelHandle, "_SourceTexture", target);
        hsvShader.SetTexture(kernelHandle, "_ResultTexture", result);
        hsvShader.SetFloats("_Resolution", new float[] { width, height });
        hsvShader.SetFloat("_HueF", hueF);
        hsvShader.SetFloat("_SaturationF", saturationF);
        hsvShader.SetFloat("_ValueF", valueF);
        hsvShader.SetBool("_Mode", mode);

        // ComputeShader 실행
        hsvShader.Dispatch(kernelHandle, width / 32, height / 32, 1);

        // 결과 텍스처 읽기
        Texture2D outputTexture = new Texture2D(width, height);
        RenderTexture.active = result;
        outputTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        outputTexture.Apply();
        RenderTexture.active = null;

        return outputTexture;
    }*/

    private void OnGUI()
    {
        GUI.skin.label.fontSize = 25;
        GUILayout.Label("Texture Color Tool.");
        GUI.skin.label.fontSize = 10;
        GUI.skin.label.alignment = TextAnchor.MiddleRight;
        GUILayout.Label("V3 by Rera*C");
        GUI.skin.label.alignment = TextAnchor.MiddleLeft;

        EditorGUILayout.Space(10);

        // Section 1: Select Texture
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("1. Select Texture", EditorStyles.boldLabel);
        TargetTexture = (Texture2D)EditorGUILayout.ObjectField("Texture:", TargetTexture, typeof(Texture2D), false);
        if (TargetTexture && !TargetTexture.isReadable)
        {
            EditorGUILayout.HelpBox("The Texture is not readable. Please check 'Advanced - Read/Write Enabled' of the texture.", MessageType.Error);
            if (GUILayout.Button("Select Texture"))
            {
                Selection.objects = new UnityEngine.Object[] { TargetTexture };
            }
        }
        EditorGUILayout.EndVertical();

        if (TargetTexture)
        {
            GUILayout.Space(10);

            // Section 2: Adjust HSV
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("2. Adjust HSV", EditorStyles.boldLabel);
            bool newMode = EditorGUILayout.Toggle("Mode (Add/Multiply)", Mode);
            autoApply = EditorGUILayout.Toggle("Auto Apply", autoApply); // Add autoApply checkbox

            // Reset HSV values if the mode has changed
            if (newMode != Mode)
            {
                ResetHSVValues(newMode);
                Mode = newMode;
            }

            // Update Hf, Sf, Vf with sliders
            if (Mode)
            {
                float newHf = EditorGUILayout.Slider("Hue (Multiply):", Hf, 0, 2);
                float newSf = EditorGUILayout.Slider("Saturation (Multiply):", Sf, 0, 2);
                float newVf = EditorGUILayout.Slider("Value (Multiply):", Vf, 0, 2);

                // Check for changes and apply immediately if autoApply is enabled
                if (autoApply && (newHf != Hf || newSf != Sf || newVf != Vf) && !isProcessing)
                {
                    Hf = newHf;
                    Sf = newSf;
                    Vf = newVf;
                    ApplyChanges();
                }
                else
                {
                    // Update the values for manual application
                    Hf = newHf;
                    Sf = newSf;
                    Vf = newVf;
                }
            }
            else
            {
                float newHf = EditorGUILayout.Slider("Hue (Add):", Hf, -1, 1);
                float newSf = EditorGUILayout.Slider("Saturation (Add):", Sf, -1, 1);
                float newVf = EditorGUILayout.Slider("Value (Add):", Vf, -1, 1);

                // Check for changes and apply immediately if autoApply is enabled
                if (autoApply && (newHf != Hf || newSf != Sf || newVf != Vf) && !isProcessing)
                {
                    Hf = newHf;
                    Sf = newSf;
                    Vf = newVf;
                    ApplyChanges();
                }
                else
                {
                    // Update the values for manual application
                    Hf = newHf;
                    Sf = newSf;
                    Vf = newVf;
                }
            }

            GUILayout.Space(10);
            if (!autoApply)
            {
                if (GUILayout.Button("Apply"))
                {
                    ApplyChanges();
                }
            }
            EditorGUILayout.EndVertical();

            if (ResultTexture)
            {
                GUILayout.Space(10);

                // Section 3: Preview and Save
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label("3. Preview and Save", EditorStyles.boldLabel);

                GUILayout.BeginVertical();
                GUILayout.Label(ResultTexture, GUILayout.Width(position.width - 40), GUILayout.Height(position.width - 40));
                GUILayout.EndVertical();

                GUILayout.Space(10);
                if (GUILayout.Button("Save As..."))
                {
                    var path = EditorUtility.SaveFilePanel("Save Texture...", "", TargetTexture.name + "_modified.png", "png");
                    if (path.Length != 0)
                    {
                        var pngData = ResultTexture.EncodeToPNG();
                        if (pngData != null)
                        {
                            File.WriteAllBytes(path, pngData);
                            AssetDatabase.Refresh();
                        }
                    }
                }
                EditorGUILayout.EndVertical();
            }
        }
    }

    private void ApplyChanges()
    {
        if (isProcessing)
        {
            Debug.LogWarning("Processing is already in progress. Please wait.");
            return; // Exit if already processing
        }

        isProcessing = true; // Set the flag to true

        if (ResultTexture)
        {
            DestroyImmediate(ResultTexture);
        }

        if (TargetTexture.isReadable)
        {
            // Show progress bar only if not auto applying
            if (!autoApply)
            {
                EditorUtility.DisplayProgressBar("Processing", "Please wait while processing the texture...", 0f);
            }

            //ResultTexture = HSVFilter(TargetTexture, Hf, Sf, Vf, Mode);
            ResultTexture = HSVFilter(TargetTexture, Hf, Sf, Vf, Mode);

            // Clear progress bar after processing
            if (!autoApply)
            {
                EditorUtility.ClearProgressBar();
            }
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "The Texture is not readable. Please check 'Advanced - Read/Write Enabled' of the texture.", "OK");
            Selection.objects = new UnityEngine.Object[] { TargetTexture };
        }

        isProcessing = false; // Reset the flag
    }

    private void ResetHSVValues(bool newMode)
    {
        if (newMode) // Multiply mode
        {
            Hf = 1; // Default to 1 for Multiply
            Sf = 1; // Default saturation to 1
            Vf = 1; // Default value to 1
        }
        else // Add mode
        {
            Hf = 0; // Default to 0 for Add
            Sf = 0; // Default saturation to 0
            Vf = 0; // Default value to 0
        }
    }
}
