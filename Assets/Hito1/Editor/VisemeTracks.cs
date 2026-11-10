using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SojaExiles
{
    // Pistas de visemas para las voces de la tia, sacadas del texto de cada
    // frase (docs/VOCES.md) y de su audio (Resources/Hito1Audio). El espanol
    // se pronuncia casi como se escribe: cada letra (o par de letras) pasa a
    // uno de los 15 visemas de Oculus que trae el avatar de Avaturn, y la
    // secuencia se reparte sobre los tramos del audio donde hay voz. Las
    // vocales duran mas que las consonantes. Resultado:
    // Resources/Hito1Anim/VisemasVoces.json, que lee PatientFace.
    // Correr de nuevo (menu Hito1 > Generar visemas de las voces) al cambiar
    // un texto o un audio.
    public static class VisemeTracks
    {
        private const string VoicesDoc = "docs/VOCES.md";
        private const string AudioFolder = "Assets/Hito1/Resources/Hito1Audio";
        private const string OutPath = "Assets/Hito1/Resources/Hito1Anim/VisemasVoces.json";
        private const float Frame = 0.01f;

        // Mismo orden que PatientFace.Visemes.
        private const int Sil = 0, PP = 1, FF = 2, TH = 3, DD = 4, KK = 5, CH = 6, SS = 7, NN = 8, RR = 9,
            AA = 10, E = 11, I = 12, O = 13, U = 14;

        [MenuItem("Hito1/Generar visemas de las voces")]
        public static void Run()
        {
            Dictionary<string, string> texts = ReadTexts();
            PatientFace.VisemeData data = new PatientFace.VisemeData();
            int done = 0;
            foreach (KeyValuePair<string, string> pair in texts)
            {
                AudioClip clip = LoadClip(pair.Key);
                if (clip == null)
                {
                    continue;
                }

                PatientFace.VisemeTrack track = Build(pair.Key, pair.Value, clip);
                if (track != null)
                {
                    data.tracks.Add(track);
                    done++;
                }
            }

            File.WriteAllText(OutPath, JsonUtility.ToJson(data, false), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(OutPath);
            Debug.Log("[VisemeTracks] " + done + " voces con visemas en " + OutPath);
        }

        // Archivo `voz_*` -> texto, de las tablas de VOCES.md.
        private static Dictionary<string, string> ReadTexts()
        {
            Dictionary<string, string> texts = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(VoicesDoc))
            {
                if (!line.StartsWith("| `voz_"))
                {
                    continue;
                }

                string[] cols = line.Split('|');
                if (cols.Length < 4)
                {
                    continue;
                }

                string name = cols[1].Trim().Trim('`');
                if (name.Contains(",") || texts.ContainsKey(name))
                {
                    continue;
                }

                // Tablas de 4 columnas: Archivo | Cuando | Texto | ElevenLabs.
                // De 3: Archivo | Quien/Cuando | `[etiqueta] texto`.
                string text = cols[3];
                text = Regex.Replace(text, @"\[[^\]]*\]", " ");
                text = Regex.Replace(text, @"\([^)]*\)", " ");
                text = text.Replace("`", " ").Trim();
                if (text.Length < 2 || text.Contains("Transcribir"))
                {
                    continue;
                }

                texts[name] = text;
            }

            return texts;
        }

        private static AudioClip LoadClip(string name)
        {
            foreach (string ext in new[] { ".wav", ".mp3", ".ogg" })
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ext);
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private static PatientFace.VisemeTrack Build(string name, string text, AudioClip clip)
        {
            List<int> phones = new List<int>();
            List<float> weights = new List<float>();
            ToVisemes(text, phones, weights);
            if (phones.Count == 0)
            {
                return null;
            }

            // Volumen por tramos de 10 ms.
            clip.LoadAudioData();
            float[] samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
            {
                Debug.LogWarning("[VisemeTracks] No se pudo leer " + name);
                return null;
            }

            int perFrame = Mathf.Max(1, Mathf.RoundToInt(clip.frequency * Frame)) * clip.channels;
            int frames = samples.Length / perFrame;
            float[] rms = new float[frames];
            float max = 0f;
            for (int f = 0; f < frames; f++)
            {
                double sum = 0;
                for (int s = 0; s < perFrame; s++)
                {
                    float v = samples[f * perFrame + s];
                    sum += v * v;
                }

                rms[f] = Mathf.Sqrt((float)(sum / perFrame));
                max = Mathf.Max(max, rms[f]);
            }

            if (max < 1e-4f)
            {
                return null;
            }

            // Con voz: por encima del 12 % del maximo.
            bool[] voiced = new bool[frames];
            int voicedCount = 0;
            for (int f = 0; f < frames; f++)
            {
                voiced[f] = rms[f] > max * 0.12f;
                if (voiced[f])
                {
                    voicedCount++;
                }
            }

            if (voicedCount == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            foreach (float w in weights)
            {
                totalWeight += w;
            }

            // Recorre los tramos con voz y va soltando los visemas segun su
            // peso; en los silencios, boca cerrada.
            PatientFace.VisemeTrack track = new PatientFace.VisemeTrack { clip = name };
            float perUnit = voicedCount / totalWeight;
            int phone = 0;
            float left = weights[0] * perUnit;
            int last = -1;
            for (int f = 0; f < frames; f++)
            {
                int v;
                if (!voiced[f])
                {
                    v = Sil;
                }
                else
                {
                    while (left <= 0f && phone < phones.Count - 1)
                    {
                        phone++;
                        left += weights[phone] * perUnit;
                    }

                    v = phones[phone];
                    left -= 1f;
                }

                if (v != last)
                {
                    track.times.Add(f * Frame);
                    track.visemes.Add(v);
                    last = v;
                }
            }

            return track;
        }

        // Texto en espanol -> visemas (con su duracion relativa).
        private static void ToVisemes(string text, List<int> phones, List<float> weights)
        {
            string t = text.ToLowerInvariant();
            int i = 0;
            bool lastWasSpace = true;
            while (i < t.Length)
            {
                char c = t[i];
                char n = i + 1 < t.Length ? t[i + 1] : ' ';
                if (!char.IsLetter(c))
                {
                    // Pausa corta entre palabras y mas larga en la puntuacion.
                    if (!lastWasSpace && (c == ',' || c == '.' || c == '…' || c == '!' || c == '?'))
                    {
                        Add(phones, weights, Sil, 1.2f);
                    }

                    lastWasSpace = true;
                    i++;
                    continue;
                }

                lastWasSpace = false;
                switch (c)
                {
                    case 'a': case 'á': Add(phones, weights, AA, 1.8f); break;
                    case 'e': case 'é': Add(phones, weights, E, 1.6f); break;
                    case 'i': case 'í': Add(phones, weights, I, 1.5f); break;
                    case 'o': case 'ó': Add(phones, weights, O, 1.7f); break;
                    case 'u': case 'ú': case 'ü': Add(phones, weights, U, 1.5f); break;
                    case 'b': case 'v': case 'p': case 'm': Add(phones, weights, PP, 1f); break;
                    case 'f': Add(phones, weights, FF, 1f); break;
                    case 't': case 'd': Add(phones, weights, DD, 0.9f); break;
                    case 'n': case 'ñ': case 'l': Add(phones, weights, NN, 0.9f); break;
                    case 'r': Add(phones, weights, RR, n == 'r' ? 1.2f : 0.8f); if (n == 'r') { i++; } break;
                    case 's': case 'z': Add(phones, weights, SS, 1f); break;
                    case 'x': Add(phones, weights, KK, 0.7f); Add(phones, weights, SS, 0.8f); break;
                    case 'j': case 'k': case 'g': Add(phones, weights, KK, 0.9f); if (c == 'g' && n == 'u') { i++; } break;
                    case 'q': Add(phones, weights, KK, 0.9f); if (n == 'u') { i++; } break;
                    case 'c':
                        if (n == 'h') { Add(phones, weights, CH, 1f); i++; }
                        else if (n == 'e' || n == 'i' || n == 'é' || n == 'í') { Add(phones, weights, SS, 1f); }
                        else { Add(phones, weights, KK, 0.9f); }
                        break;
                    case 'y':
                        // "y" sola o al final suena como "i".
                        bool vowelNext = "aeiouáéíóú".IndexOf(n) >= 0;
                        Add(phones, weights, vowelNext ? CH : I, vowelNext ? 0.9f : 1.3f);
                        break;
                    case 'h': break; // muda
                    default: Add(phones, weights, NN, 0.8f); break;
                }

                if (c == 'l' && n == 'l')
                {
                    phones[phones.Count - 1] = CH;
                    i++;
                }

                i++;
            }
        }

        private static void Add(List<int> phones, List<float> weights, int viseme, float weight)
        {
            phones.Add(viseme);
            weights.Add(weight);
        }
    }
}
