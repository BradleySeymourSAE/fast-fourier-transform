 using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FFT : MonoBehaviour
{
    public GameObject thing;
    private Texture2D tex;
    private AudioSource audio;
    public GameObject gob;
    private float time = 0;
    private const int sampleCount = 512;
    private const int rowCount = 2048;
    private bool saved = false;
    public string saveName;
    public GameObject waves;

    private Material wavemat;
    // Start is called before the first frame update
    private void Start()
    {
        //wavemat = waves.GetComponent<MeshRenderer>().material;
        tex = new Texture2D(rowCount, sampleCount);
        tex.filterMode = FilterMode.Point;
        audio = GetComponent<AudioSource>();
        gob.GetComponent<MeshRenderer>().material.mainTexture = tex;
        for (int t = 0; t < rowCount; ++t)
        {
            for (int f = 0; f < sampleCount; ++f)
            {
                    tex.SetPixel(t, f, new Color(0, 0, 0));
            }
        }
        tex.Apply();
        audio.PlayDelayed(1.0f);
    }

    // Update is called once per frame
    private void Update()
    {
        float[] fft0 = new float[sampleCount];
        float[] fft1 = new float[sampleCount];
        audio.GetSpectrumData(fft0, 0, FFTWindow.Rectangular);
        audio.GetSpectrumData(fft1, 1, FFTWindow.Rectangular);
        float[] ffts = new float[16];
        for(int i=0;i<16;++i)
        {
            ffts[i] = fft0[i];
        }
        //wavemat.SetFloatArray("_FFT", ffts);

        int x = (int)((audio.time / audio.clip.length) * (rowCount-1));
        if(x>rowCount-1 || !audio.isPlaying)
        {
            if (saved)
                return;
            else
            {
                saved = true;
                byte[] png = tex.EncodeToPNG();
                System.IO.File.WriteAllBytes(saveName, png);
            }
        }
        for (int f = 0; f < sampleCount; ++f)
        {
            float a0 = Mathf.Pow(fft0[f], 0.5f);
            float a1 = Mathf.Pow(fft1[f], 0.5f);
            Color c = tex.GetPixel(x, f);
            if (a0 > c.r)
            {
                c.r = a0;
                c.b = a0;
            }
            if (a1 > c.g)
            {
                c.g = a1;
            }
            tex.SetPixel(x, f, c);
        }
        //tex.SetPixel(x, sampleCount - 1, new Color(0, 1, 0));
        //if(x%100==0) 
            tex.Apply();
        Camera.main.transform.position = new Vector3((audio.time / audio.clip.length) * 10.0f,0.5f,0.25f);
        time += Time.fixedDeltaTime;


        float a = 0;
        for(int i=0;i<512;++i)
        {
            if (fft0[i] > a)
                a = fft0[i];
        }
        a = a + 1;
        thing.transform.localScale = new Vector3(a, a, a);
        thing.GetComponent<MeshRenderer>().material.color = new Color(fft0[15]*4.0f, fft0[17] * 4.0f, fft0[20] * 4.0f);

    }
}
