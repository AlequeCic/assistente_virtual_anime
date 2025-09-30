using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Conversões simples WAV <-> AudioClip (16-bit PCM).
/// Testado para WAV PCM 16 bit.
/// </summary>
public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip == null) return null;

        int channels = clip.channels;
        int sampleRate = clip.frequency;
        int samplesPerChannel = clip.samples;
        int totalSamples = samplesPerChannel * channels;

        float[] data = new float[totalSamples];
        clip.GetData(data, 0);

        // converte floats [-1,1] para Int16
        short[] intData = new short[totalSamples];
        byte[] bytesData = new byte[totalSamples * 2];
        int rescaleFactor = 32767; // to convert float to Int16

        for (int i = 0; i < totalSamples; i++)
        {
            intData[i] = (short)(Mathf.Clamp(data[i], -1f, 1f) * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            bytesData[i * 2] = byteArr[0];
            bytesData[i * 2 + 1] = byteArr[1];
        }

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            // RIFF header
            writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(36 + bytesData.Length); // ChunkSize
            writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

            // fmt subchunk
            writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16); // Subchunk1Size for PCM
            writer.Write((short)1); // AudioFormat = 1 (PCM)
            writer.Write((short)channels);
            writer.Write(sampleRate);
            int byteRate = sampleRate * channels * 2; // 16 bits = 2 bytes
            writer.Write(byteRate);
            short blockAlign = (short)(channels * 2);
            writer.Write(blockAlign);
            writer.Write((short)16); // bits per sample

            // data subchunk
            writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            writer.Write(bytesData.Length);
            writer.Write(bytesData, 0, bytesData.Length);

            writer.Flush();
            return stream.ToArray();
        }
    }

    public static AudioClip ToAudioClip(byte[] wavFile)
    {
        // Encontrar "data" chunk
        int channels = BitConverter.ToInt16(wavFile, 22);
        int sampleRate = BitConverter.ToInt32(wavFile, 24);
        short bitsPerSample = BitConverter.ToInt16(wavFile, 34);

        // achar posição do 'data' string no header
        int pos = 12;
        while (pos < wavFile.Length)
        {
            string chunkID = System.Text.Encoding.UTF8.GetString(wavFile, pos, 4);
            int chunkSize = BitConverter.ToInt32(wavFile, pos + 4);
            if (chunkID == "data")
            {
                pos += 8;
                int dataSize = chunkSize;
                int totalSamples = dataSize / (bitsPerSample / 8);
                float[] floatArr = new float[totalSamples];

                int i = 0;
                if (bitsPerSample == 16)
                {
                    for (int offset = pos; offset < pos + dataSize; offset += 2)
                    {
                        short sample = BitConverter.ToInt16(wavFile, offset);
                        floatArr[i++] = sample / 32768f;
                    }
                }
                else
                {
                    throw new Exception("Only 16-bit WAV supported in this helper.");
                }

                int samplesPerChannel = totalSamples / channels;
                AudioClip audioClip = AudioClip.Create("wav", samplesPerChannel, channels, sampleRate, false);
                audioClip.SetData(floatArr, 0);
                return audioClip;
            }
            pos += 8 + chunkSize;
        }

        throw new Exception("data chunk not found in WAV file");
    }
}
