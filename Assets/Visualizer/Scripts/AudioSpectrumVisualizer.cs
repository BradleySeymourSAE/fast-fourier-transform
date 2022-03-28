using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class AudioSpectrumVisualizer : MonoBehaviour
{
	
	public enum Visualizations
	{
		Line,
		Circle,
		ExpansibleCircle,
		Sphere,
		Square
	};
	public enum ScaleFrom
	{
		Center,
		Downside
	};

	public enum Channels
	{
		n512,
		n1024,
		n2048,
		n4096,
		n8192
	}

	private bool m_HasColorValueUpdated;
	private static readonly int s_TintColor = Shader.PropertyToID("_TintColor");
	public GameObject SoundBarPrefab;
	public GameObject DownsideSoundBarPrefab;
	public Transform SoundBarsParent;

	public bool UseListenToAllSounds;
	public AudioSource Source;

	[Space(5), Range(32, 256)] public int SoundBarQuantity = 100;

	private List<GameObject> m_SoundBars = new List<GameObject>();
	private int m_NumberUsedSoundBars = 100;

	public ScaleFrom scaleFrom = ScaleFrom.Downside;

	[Range(0.1f, 20f)] public float SoundBarWidth = 3f;

	[Range(0.1f, 20f)] public float SoundBarDepth = 1f;

	public Transform center;
	public bool AllowCameraControl = true;
	public bool AllowCameraRotation = true;
	public bool UseDefault = true;

	[Range(-35, 35)] public float Velocity = 15f;
	[Range(0, 200f)] public float Height = 40f;
	[Range(0, 500)] public float OrbitalDistance = 300f;
	[Range(1, 179)] public int FOV = 60;

	public bool UseMirrorVisualisation = true;
	public bool ScaleAudioByRhythm;

	[Range(10, 200f)] public float Length = 65f;

	public Visualizations visualization = Visualizations.Line;

	[Range(1f, 100f)] public float ExtraVelocityScaling = 50f;
	[Range(0.75f, 15f)] public float GlobalVisualisationScale = 4f;
	[Range(0f, 5f)] public float MinimumHeightThreshold = 1.5f;
	[Range(1, 15)] public int SmoothingVelocity = 3;

	public Channels AudioChannels = Channels.n4096;
	public FFTWindow FFTMethod = FFTWindow.Blackman;
	private int m_ChannelValue = 2048;

	public ParticleSystem RhythmParticleSystem;
	public bool UseAutoRhythmParticles = true;

	[Range(0f, 100f)] public float RhythmSensitivity = 30;
	
	private const float K_MINIMUM_RHYTHM_SENSITIVITY = 1.5f;

	[Range(1, 150)] public int TotalParticlesEmitCount = 100;
	[Range(0.01f, 1f)] public float RhythmParticlesMaxInterval = 0.25f;

	private float m_RemainingRhythmParticlesTime;
	private bool m_HasSurpassedRhythmTime;

	[Range(1f, 300f)] public float BassSensitivity = 60f;
	[Range(0.5f, 2f)] public float BassHeight = 1.5f;
	[Range(1, 5)] public int BassHorizontalScalingFactor = 1;
	[Range(0, 256)] public int BassOffset;
	[Range(1f, 300f)] public float TrebleSensitivity = 120f;
	[Range(0.5f, 2f)] public float TrebleHeight = 1.35f;

	[Range(1, 5)] public int TrebleHorizontalScale = 3;
	[Range(0, 256)] public int TrebleOffset = 40;
	public bool UseSoundBarParticles = true;

	[Range(0f, 0.1f)]
	public float ParticlesMaxInterval = 0.02f;

	private float m_ParticlesTimeRemaining;
	private bool m_HasSurpassedTime;

	[Range(0.1f, 2f)] public float MinimumParticleSensitivity = 1.3f;

	public bool UseLerpColor = true;
	public Color[] Colors = new Color[9];

	[Range(0.1f, 5f)] public float ColorTimeInterval = 3f;

	[Range(0.1f, 5f)] public float ColorLerpTime = 2f;

	public bool UseGradient;
	public Gradient CustomGradient;
	public Color RhythmParticleSystemColor = Color.white;

	[Range(0f, 2f)] public float VisualizationRaysLength = 0.7f;
	[Range(0f, 1f)] public float VisualisationRaysAlpha = 0.8f;

	private int m_PositionColorIndex;
	[HideInInspector] public Color m_CurrentActualColor;

	private Vector3 m_PreviousLeftScale;
	private Vector3 m_PreviousRightScale;
	private Vector3 m_RightScale;
	private Vector3 m_LeftScale;
	private float m_Timer;
	private int m_HalfBars;
	private int m_VisualizationCounter = 1;
	private float m_NextLeftScale;
	private float m_NextRightScale;
	private float m_RhythmAverage;
	private Visualizations m_PreviousLineVisualisation = Visualizations.Line;
	private bool m_CanUpdateVisualizations;

	
	
	public void EmitOnRhythm()
	{
		#pragma warning disable 618
		float[] spectrumLeftData = Source.GetSpectrumData(m_ChannelValue, 0, FFTMethod);
		float[] spectrumRightData = Source.GetSpectrumData(m_ChannelValue, 1, FFTMethod);
		#pragma warning restore 618

		int number = 0;
		float sum = 0;
		
		for (int i = 0; i < 40; i++)
		{
			sum += Mathf.Max(spectrumLeftData[i], spectrumRightData[i]);
			number++;
		}
		m_RhythmAverage = (sum / number) * RhythmSensitivity;
		if (m_RhythmAverage >= K_MINIMUM_RHYTHM_SENSITIVITY)
		{
			m_HasSurpassedRhythmTime = true;
		}
		if (UseAutoRhythmParticles)
		{
			if (m_HasSurpassedRhythmTime)
			{
				RhythmParticleSystem.Emit(TotalParticlesEmitCount);
			}
		}
	}

	public void Restart()
	{
		m_HasColorValueUpdated = false;
		m_CanUpdateVisualizations = false;

		if (m_SoundBars.Count > 0)
		{
			for (int i = 0; i < m_SoundBars.Count; i++)
			{
				DestroyImmediate(m_SoundBars[i]);
			}
		}

		m_SoundBars.Clear();

		Application.targetFrameRate = 144;

		// Check the prefabs
		if ((SoundBarPrefab != null) && (DownsideSoundBarPrefab != null))
		{

			if (SoundBarQuantity % 4 != 0)
			{
				SoundBarQuantity += SoundBarQuantity % 4;
			}

			if (SoundBarQuantity < 32)
			{
				SoundBarQuantity = 32;
			}
			else if (SoundBarQuantity > 256)
			{
				SoundBarQuantity = 256;
			}

			m_NumberUsedSoundBars = SoundBarQuantity;
			m_HalfBars = m_NumberUsedSoundBars / 2;

			CreateCubes();

		}
		else
		{
			Debug.LogWarning("Please assign Sound Bar Prefabs to the script");
			enabled = false;
		}

	}

	/// <summary>
	/// Awake this instance.
	/// </summary>
	private void Awake()
	{
		Restart();
	}

	/// <summary>
	/// Creates the cubes.
	/// </summary>
	private void CreateCubes()
	{

		float newRayScale = (VisualizationRaysLength * 5);
		float newWidth = SoundBarWidth - 1;
		GameObject soundBarToInstantiate;

		if (scaleFrom == ScaleFrom.Center)
		{
			soundBarToInstantiate = SoundBarPrefab;
			newWidth = (newWidth / 2f) - 0.5f;
		}
		else
		{
			soundBarToInstantiate = DownsideSoundBarPrefab;
		}

		for (int i = 0; i < m_NumberUsedSoundBars; i++)
		{

			GameObject clone = Instantiate(soundBarToInstantiate, transform.position, Quaternion.identity);
			clone.transform.SetParent(SoundBarsParent.transform);
			clone.GetComponent<SoundSpectrumBar>().AttachedCubeRenderer.transform.localScale = new Vector3(SoundBarWidth, 1, SoundBarDepth);

			clone.name = $"SoundBar {i + 1}";

			var renderers = clone.GetComponentsInChildren<Renderer>();

			Color newColor = Colors[0];
			Color newColor2 = newColor;
			newColor2.a = VisualisationRaysAlpha;

			if (UseGradient)
			{
				newColor = CustomGradient.Evaluate(((i + 1) / (float) m_NumberUsedSoundBars));


				var rhythmParticleS = RhythmParticleSystem.main;
				rhythmParticleS.startColor = RhythmParticleSystemColor;
			}

			foreach (Renderer rend in renderers)
			{
				rend.material.color = newColor;
			}

			var actualParticleSystem = clone.GetComponentInChildren<ParticleSystem>().main;
			actualParticleSystem.startColor = newColor;

			clone.GetComponent<SoundSpectrumBar>().AttachedRaycastRenderer.material.SetColor(s_TintColor, newColor2);

			if (scaleFrom == ScaleFrom.Downside)
			{
				clone.GetComponent<SoundSpectrumBar>().AttachedRaycastRenderer.transform.localScale = new Vector3(Mathf.Clamp(newWidth, 1, Mathf.Infinity), 1, VisualizationRaysLength);
				clone.GetComponent<SoundSpectrumBar>().AttachedRaycastRenderer.transform.localPosition = new Vector3(0, newRayScale, 0);
			}
			else
			{
				clone.GetComponent<SoundSpectrumBar>().AttachedRaycastRenderer.transform.localScale = new Vector3(Mathf.Clamp(newWidth, 0.5f, Mathf.Infinity), 1, VisualizationRaysLength);
			}

			m_SoundBars.Add(clone);
		}

		m_CanUpdateVisualizations = true;
		UpdateVisualizations();
	}

	/// <summary>
	/// Change to the next form. TRUE = Next, FALSE = PREVIOUS
	/// </summary>
	/// <param name="next">If set to <c>true</c> next.</param>
	public void NextForm(bool next)
	{
		if (next)
		{
			m_VisualizationCounter++;
		}
		else
		{
			m_VisualizationCounter--;
		}

		if (m_VisualizationCounter > 5)
		{
			m_VisualizationCounter = 1;
		}
		else if (m_VisualizationCounter <= 0)
		{
			m_VisualizationCounter = 5;
		}

		if (m_VisualizationCounter == 1)
		{
			visualization = Visualizations.Line;
		}
		else if (m_VisualizationCounter == 2)
		{
			visualization = Visualizations.Circle;
		}
		else if (m_VisualizationCounter == 3)
		{
			visualization = Visualizations.ExpansibleCircle;
		}
		else if (m_VisualizationCounter == 4)
		{
			visualization = Visualizations.Sphere;
		}

		UpdateVisualizations();
	}

	/// <summary>
	/// Updates the channels of audio.
	/// </summary>
	private void UpdateChannels()
	{
		if (AudioChannels == Channels.n512)
		{
			m_ChannelValue = 512;
		}
		else if (AudioChannels == Channels.n1024)
		{
			m_ChannelValue = 1024;
		}
		else if (AudioChannels == Channels.n2048)
		{
			m_ChannelValue = 2048;
		}
		else if (AudioChannels == Channels.n4096)
		{
			m_ChannelValue = 4096;
		}
		else if (AudioChannels == Channels.n8192)
		{
			m_ChannelValue = 8192;
		}
	}
	
	
	private void CameraPosition()
	{
		switch(visualization)
		{
			case Visualizations.Line:
			{
				Camera.main.fieldOfView = FOV;
				var cameraPos = transform.position;
				cameraPos.z -= 170f;
				Camera.main.transform.position = cameraPos;
				cameraPos.y += 5f + Height;
				Camera.main.transform.position = cameraPos;
				Camera.main.transform.LookAt(center);
				break;
			}
			case Visualizations.Circle:
			{
				Camera.main.fieldOfView = FOV;
				var cameraPos = transform.position;
				cameraPos.y += ((1f + Height) / 20f);
				cameraPos.z += 5f;
				Camera.main.transform.position = cameraPos;

				Camera.main.transform.LookAt(SoundBarsParent.position);
				break;
			}
			case Visualizations.ExpansibleCircle:
			{
				Camera.main.fieldOfView = FOV;
				var cameraPos = transform.position;
				cameraPos.y += 55f;
				Camera.main.transform.position = cameraPos;
				Camera.main.transform.LookAt(SoundBarsParent.position);
				break;
			}
			case Visualizations.Sphere:
			{
				Camera.main.fieldOfView = FOV;
				var cameraPos = transform.position;
				cameraPos.z -= 40f;
				cameraPos.y += 5f + Height;

				Camera.main.transform.position = cameraPos;

				Camera.main.transform.LookAt(SoundBarsParent.position);
				Camera.main.transform.position = cameraPos;
				break;
			}
			case Visualizations.Square:
			{
				Camera.main.fieldOfView = FOV;
				var cameraPos = transform.position;
				cameraPos.z -= 40f;
				cameraPos.y += 5f + Height;

				Camera.main.transform.position = cameraPos;

				Camera.main.transform.LookAt(SoundBarsParent.position);
				Camera.main.transform.position = cameraPos;
				break;
			}
		}

	}

	private void SetVisualizationPredefinedValues()
	{
		switch(visualization)
		{
			case Visualizations.Line:
				ScaleAudioByRhythm = false;
				UseLerpColor = false;
				Length = 65;
				Height = 40;
				OrbitalDistance = 300;
				break;
			case Visualizations.Circle:
				ScaleAudioByRhythm = false;
				UseLerpColor = false;
				Length = 125;
				Height = 40;
				OrbitalDistance = 250;
				break;
			case Visualizations.ExpansibleCircle:
				ScaleAudioByRhythm = false;
				UseLerpColor = false;
				Length = 100;
				Height = 40;
				OrbitalDistance = 275;
				break;
			case Visualizations.Sphere:
				ScaleAudioByRhythm = true;
				UseLerpColor = true;
				Length = 65;
				Height = 15;
				OrbitalDistance = 220;
				Restart();
				break;
			case Visualizations.Square:
				ScaleAudioByRhythm = false;
				UseLerpColor = false;
				Length = 35;
				Height = 15;
				OrbitalDistance = 250;

				Restart();
				break;
		}
	}

	/// <summary>
	/// Camera Rotating Around Movement.
	/// </summary>
	private void CameraMovement()
	{
		Camera.main.transform.position = center.position + (Camera.main.transform.position - center.position).normalized * OrbitalDistance;

		if (AllowCameraRotation)
		{
			Camera.main.transform.RotateAround(center.position, Vector3.up, -Velocity * Time.deltaTime);
		}
	}
	
	private Color m_CurrentColor;
	
	private void ChangeColor()
	{

		m_CurrentColor = m_SoundBars[0].GetComponent<SoundSpectrumBar>().AttachedCubeRenderer.material.color;

		m_CurrentActualColor = Color.Lerp(m_CurrentColor, Colors[m_PositionColorIndex], Time.deltaTime / ColorLerpTime);

		foreach (GameObject cube in m_SoundBars)
		{
			var newColor = m_CurrentActualColor;
			newColor.a = VisualisationRaysAlpha;
			cube.GetComponent<SoundSpectrumBar>().AttachedRaycastRenderer.material.SetColor(s_TintColor, newColor);
			cube.GetComponent<SoundSpectrumBar>().AttachedCubeRenderer.material.color = m_CurrentActualColor;

			var ps = cube.GetComponent<SoundSpectrumBar>().AttachedParticleSystem.main;
			ps.startColor = m_CurrentActualColor;

			var actualParticleSystem = RhythmParticleSystem.main;
			actualParticleSystem.startColor = m_CurrentActualColor;
		}
	}

	/// <summary>
	/// Change SoundBars and Particles Color Helper.
	/// </summary>
	private void NextColor()
	{
		m_Timer = ColorTimeInterval;
		UseLerpColor = false;
		if (m_PositionColorIndex < Colors.Length - 1)
		{
			m_PositionColorIndex++;
		}
		else
		{
			m_PositionColorIndex = 0;
		}
		UseLerpColor = true;
	}
	
	public void UpdateVisualizations()
	{
		if (!m_CanUpdateVisualizations)
		{
			return;
		}

		switch(visualization)
		{
			// Visualizations
			case Visualizations.Circle:
			{
				for (int i = 0; i < m_NumberUsedSoundBars; i++)
				{
					float angle = i * Mathf.PI * 2f / m_NumberUsedSoundBars;
					Vector3 pos = SoundBarsParent.transform.localPosition;
					pos -= new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Length;
					m_SoundBars[i].transform.localPosition = pos;
					m_SoundBars[i].transform.LookAt(SoundBarsParent.position);

					var rot = m_SoundBars[i].transform.eulerAngles;
					rot.x = 0;
					m_SoundBars[i].transform.localEulerAngles = rot;
				}
				break;
			}
			case Visualizations.Line:
			{
				for (int i = 0; i < m_NumberUsedSoundBars; i++)
				{
					Vector3 pos = SoundBarsParent.transform.localPosition;
					pos.x -= Length * 5;
					pos.x += (Length / m_NumberUsedSoundBars) * (i * 10);

					m_SoundBars[i].transform.localPosition = pos;
					m_SoundBars[i].transform.localEulerAngles = Vector3.zero;
				}
				break;
			}
			case Visualizations.ExpansibleCircle:
			{
				for (int i = 0; i < m_NumberUsedSoundBars; i++)
				{
					float angle = i * Mathf.PI * 2f / m_NumberUsedSoundBars;
					Vector3 pos = SoundBarsParent.transform.localPosition;
					pos -= new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * Length;
					m_SoundBars[i].transform.localPosition = pos;
					m_SoundBars[i].transform.LookAt(SoundBarsParent.position);

					var newRot = m_SoundBars[i].transform.eulerAngles;
					newRot.x -= 90;

					m_SoundBars[i].transform.eulerAngles = newRot;
				}
				break;
			}
			case Visualizations.Sphere:
			{
				var points = UniformPointsOnSphere(m_NumberUsedSoundBars, Length);

				for (var i = 0; i < m_NumberUsedSoundBars; i++)
				{

					m_SoundBars[i].transform.localPosition = points[i];

					m_SoundBars[i].transform.LookAt(SoundBarsParent.position);

					var rot = m_SoundBars[i].transform.eulerAngles;
					rot.x -= 90;

					m_SoundBars[i].transform.eulerAngles = rot;
				}
				break;
			}
			case Visualizations.Square:
				try
				{
					var points = UniformPointsOnSquare(Length / 4f, m_NumberUsedSoundBars);

					for (var i = 0; i < m_NumberUsedSoundBars; i++)
					{
						m_SoundBars[i].transform.localPosition = points[i];
					}
				}
				catch
				{
					// Fix annoying error when you are changing the Soundbars quantity in playmode
					Restart();
				}
				break;
		}

		UpdateChannels();

		if (AllowCameraControl)
		{

			if (m_PreviousLineVisualisation != visualization)
			{
				m_PreviousLineVisualisation = visualization;

				m_VisualizationCounter = visualization switch
				{
					Visualizations.Line => 1,
					Visualizations.Circle => 2,
					Visualizations.ExpansibleCircle => 3,
					Visualizations.Sphere => 4,
					Visualizations.Square => 5,
					_ => m_VisualizationCounter
				};

				if (UseDefault)
				{
					SetVisualizationPredefinedValues();
				}
			}

			CameraPosition();

		}
	}

	private Vector3[] UniformPointsOnSquare(float separation, int size)
	{
		var points = new List<Vector3>();

		int realSize = size / 4;
		float otherSize = (realSize / 2f) * separation;
		float currentPos = (-otherSize) + 1;
		
		for (int i = 0; i < realSize; i++)
		{
			points.Add(new Vector3(currentPos, 0, otherSize));

			currentPos += separation;
		}
		currentPos = (otherSize) - 1;
		
		for (int i = 0; i < realSize; i++)
		{
			points.Add(new Vector3(otherSize, 0, currentPos));

			currentPos -= separation;
		}

		currentPos = (otherSize) - 1;
		
		for (int i = 0; i < realSize; i++)
		{
			points.Add(new Vector3(currentPos, 0, -otherSize));

			currentPos -= separation;
		}

		currentPos = (-otherSize) + 1;

		for (int i = 0; i < realSize; i++)
		{
			points.Add(new Vector3(-otherSize, 0, currentPos));

			currentPos += separation;
		}

		return points.ToArray();
	}

	/// <summary>
	/// Create a Sphere with the given verticles number.
	/// </summary>
	/// <returns>The points on sphere.</returns>
	/// <param name="verticlesNum">Verticles number.</param>
	/// <param name="scale">Scale.</param>
	private Vector3[] UniformPointsOnSphere(float verticlesNum, float scale)
	{
		var points = new List<Vector3>();
		var i = Mathf.PI * (3 - Mathf.Sqrt(5));
		var o = 2 / verticlesNum;
		for (var k = 0; k < verticlesNum; k++)
		{
			var y = k * o - 1 + (o / 2);
			var r = Mathf.Sqrt(1 - y * y);
			var phi = k * i;
			points.Add(new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r) * scale);
		}
		return points.ToArray();
	}
	
	private void LateUpdate()
	{
		if (UseLerpColor)
		{
			m_Timer -= Time.deltaTime;
			if (m_Timer < 0f)
			{
				NextColor();
			}
			ChangeColor();
			m_HasColorValueUpdated = false;
		}
		else
		{
			if (UseGradient)
			{
				if (!m_HasColorValueUpdated)
				{
					for (int i = 0; i < m_SoundBars.Count; i++)
					{
						Color newColor = CustomGradient.Evaluate(((i + 1) / (float) m_NumberUsedSoundBars));
						m_SoundBars[i].GetComponent<SoundSpectrumBar>().AttachedCubeRenderer.material.color = newColor;

						ParticleSystem.MainModule actualParticleSystem = m_SoundBars[i].GetComponent<SoundSpectrumBar>().AttachedParticleSystem.main;
						actualParticleSystem.startColor = newColor;
						newColor.a = VisualisationRaysAlpha;
						m_SoundBars[i].GetComponent<SoundSpectrumBar>().AttachedRaycastRenderer.material.SetColor(s_TintColor, newColor);
					}
					ParticleSystem.MainModule rhythmParticleSystem = RhythmParticleSystem.main;
					
					rhythmParticleSystem.startColor = RhythmParticleSystemColor;
					m_HasColorValueUpdated = true;
				}
			}
			else
			{
				m_HasColorValueUpdated = false;
			}
		}

		#pragma warning disable
		
		float[] spectrumLeftData;
		float[] spectrumRightData;

		if (UseListenToAllSounds)
		{
			switch(UseMirrorVisualisation)
			{
				case true:
					spectrumLeftData = AudioListener.GetSpectrumData(m_ChannelValue, 0, FFTMethod);
					spectrumRightData = AudioListener.GetSpectrumData(m_ChannelValue, 0, FFTMethod);
					break;
				default:
					spectrumLeftData = AudioListener.GetSpectrumData(m_ChannelValue, 0, FFTMethod);
					spectrumRightData = AudioListener.GetSpectrumData(m_ChannelValue, 1, FFTMethod);
					break;
			}
		}
		else
		{
			if (Source == null)
			{
				Debug.LogWarning("No AudioSource detected 'Listen All Sounds' activated");
				UseListenToAllSounds = true;
				return;
			}

			switch(UseMirrorVisualisation)
			{
				case true:
					spectrumLeftData = Source.GetSpectrumData(m_ChannelValue, 0, FFTMethod);
					spectrumRightData = Source.GetSpectrumData(m_ChannelValue, 0, FFTMethod);
					break;
				default:
					spectrumLeftData = Source.GetSpectrumData(m_ChannelValue, 0, FFTMethod);
					spectrumRightData = Source.GetSpectrumData(m_ChannelValue, 1, FFTMethod);
					break;
			}
		}
		#pragma warning restore
		if (m_RemainingRhythmParticlesTime <= 0)
		{
			int total = 0;
			float sum = 0;
			for (int i = 0; i < 40; i++)
			{
				sum += Mathf.Max(spectrumLeftData[i], spectrumRightData[i]);
				total++;
			}
			m_RhythmAverage = (sum / total) * RhythmSensitivity;
			if (m_RhythmAverage >= K_MINIMUM_RHYTHM_SENSITIVITY)
			{
				m_HasSurpassedRhythmTime = true;
			}
			if (UseAutoRhythmParticles)
			{
				if (m_HasSurpassedRhythmTime)
				{
					RhythmParticleSystem.Emit(TotalParticlesEmitCount);
				}
			}
		}
		if (!ScaleAudioByRhythm)
		{
			for (int i = 0; i < m_HalfBars; i++)
			{
				int spectrumLeft = i * BassHorizontalScalingFactor + BassOffset;
				int spectrumRight = i * TrebleHorizontalScale + TrebleOffset;
				float spectrumLeftValue = 0;
				float spectrumRightValue = 0;
				
				if (UseMirrorVisualisation)
				{
					m_PreviousLeftScale = m_SoundBars[(m_HalfBars - 1 - i)].transform.localScale;
					m_PreviousRightScale = m_SoundBars[i + m_HalfBars].transform.localScale;
					spectrumLeftValue = spectrumLeftData[spectrumLeft] * BassSensitivity;
					spectrumRightValue = spectrumLeftValue;
				}
				else
				{
					m_PreviousLeftScale = m_SoundBars[i].transform.localScale;
					m_PreviousRightScale = m_SoundBars[i + m_HalfBars].transform.localScale;
					spectrumLeftValue = spectrumLeftData[spectrumLeft] * BassSensitivity;
					spectrumRightValue = spectrumRightData[spectrumRight] * TrebleSensitivity;
				}
				m_NextLeftScale = Mathf.Lerp(m_PreviousLeftScale.y,
					spectrumLeftValue * BassHeight * GlobalVisualisationScale,
					Time.deltaTime * ExtraVelocityScaling);
				
				switch(m_NextLeftScale >= m_PreviousLeftScale.y)
				{
					case true:
						m_PreviousLeftScale.y = m_NextLeftScale;
						m_LeftScale = m_PreviousLeftScale;
						break;
					default:
						m_LeftScale = m_PreviousLeftScale;
						m_LeftScale.y = Mathf.Lerp(m_PreviousLeftScale.y, MinimumHeightThreshold, Time.deltaTime * SmoothingVelocity);
						break;
				}
				switch(UseMirrorVisualisation)
				{
					case true:
						EmitParticle((m_HalfBars - 1 - i), spectrumLeftValue);
						m_SoundBars[(m_HalfBars - 1 - i)].transform.localScale = m_LeftScale;
						break;
					default:
						EmitParticle(i, spectrumLeftValue);
						m_SoundBars[i].transform.localScale = m_LeftScale;
						break;
				}
				m_NextRightScale = UseMirrorVisualisation switch
				{
					true => m_NextLeftScale,
					_ => Mathf.Lerp(
						m_PreviousRightScale.y,
						spectrumRightValue * TrebleHeight * GlobalVisualisationScale, 
						Time.deltaTime * ExtraVelocityScaling)
				};
				
				if (m_NextRightScale >= m_PreviousRightScale.y)
				{
					m_PreviousRightScale.y = m_NextRightScale;
					m_RightScale = m_PreviousRightScale;
				}
				else
				{
					m_RightScale = m_PreviousRightScale;
					m_RightScale.y = Mathf.Lerp(m_PreviousRightScale.y, MinimumHeightThreshold, Time.deltaTime * SmoothingVelocity);
				}
				EmitParticle(i + m_HalfBars, spectrumRightValue);
				m_SoundBars[i + m_HalfBars].transform.localScale = m_RightScale;
			}
		}
		else
		{
			for (int i = 0; i < m_NumberUsedSoundBars; i++)
			{
				m_PreviousLeftScale = m_SoundBars[i].transform.localScale;
				if (m_HasSurpassedRhythmTime)
				{
					m_NextLeftScale = Mathf.Lerp(m_PreviousLeftScale.y,
						m_RhythmAverage * BassHeight * GlobalVisualisationScale,
						Time.deltaTime * SmoothingVelocity);
					
					if (UseSoundBarParticles)
					{
						if (m_ParticlesTimeRemaining <= 0f)
						{
							m_SoundBars[i].GetComponentInChildren<ParticleSystem>().Play();
							m_HasSurpassedTime = true;
						}
					}
				}
				else
				{
					m_NextLeftScale = Mathf.Lerp(m_PreviousLeftScale.y,
						m_RhythmAverage * GlobalVisualisationScale,
						Time.deltaTime * ExtraVelocityScaling);
				}
				if (m_NextLeftScale >= m_PreviousLeftScale.y)
				{
					m_PreviousLeftScale.y = m_NextLeftScale;
					m_RightScale = m_PreviousLeftScale;
				}
				else
				{
					m_RightScale = m_PreviousLeftScale;
					m_RightScale.y = Mathf.Lerp(m_PreviousLeftScale.y, MinimumHeightThreshold, Time.deltaTime * SmoothingVelocity);
				}
				m_SoundBars[i].transform.localScale = m_RightScale;
			}
		}

		if (UseSoundBarParticles)
		{
			switch(m_HasSurpassedTime)
			{
				case true:
					m_HasSurpassedTime = false;
					m_ParticlesTimeRemaining = ParticlesMaxInterval;
					break;
				default:
					m_ParticlesTimeRemaining -= Time.deltaTime;
					break;
			}
		}
		switch(m_HasSurpassedRhythmTime)
		{
			case true:
				m_HasSurpassedRhythmTime = false;
				m_RemainingRhythmParticlesTime = RhythmParticlesMaxInterval;
				break;
			default:
				m_RemainingRhythmParticlesTime -= Time.deltaTime;
				break;
		}
		if (AllowCameraControl)
		{
			CameraMovement();
		}
	}

	private void EmitParticle(int index, float spectrumValue)
	{
		if (UseSoundBarParticles)
		{
			if (spectrumValue >= MinimumParticleSensitivity)
			{
				if (m_ParticlesTimeRemaining <= 0f)
				{
					m_SoundBars[index].GetComponentInChildren<ParticleSystem>().Emit(1);
					m_HasSurpassedTime = true;
				}
			}
		}
	}


}