using UnityEngine;
using UnityEngine.Experimental.Rendering;
using ComputeShaderUtility;

public class Simulation : MonoBehaviour
{
	public enum SpawnMode { Random, Point, InwardCircle, RandomCircle }

	const int updateKernel = 0;
	const int diffuseMapKernel = 1;

	const int FrogInit = 0;
	const int FrogDisplay = 1;
	const int FrogAct = 2;
	const int FrogResolve = 3;
	const int PixelResolve = 4;
	const int PixelInit = 5;
	const int PixelDisplay = 6;

	public ComputeShader compute;
	public ComputeShader drawAgentsCS;
	public ComputeShader myCS;

	public SlimeSettings settings;
	public int numFrogs = 64;

	[Header("Display Settings")]
	public bool showAgentsOnly;
	public FilterMode filterMode = FilterMode.Point;
	public GraphicsFormat format = ComputeHelper.defaultGraphicsFormat;


	[SerializeField, HideInInspector] protected RenderTexture trailMap;
	[SerializeField, HideInInspector] protected RenderTexture diffusedTrailMap;
	[SerializeField, HideInInspector] protected RenderTexture displayTexture;

	ComputeBuffer agentBuffer;
	ComputeBuffer settingsBuffer;
	Texture2D colourMapTexture;

	ComputeBuffer frogsBuffer;
	ComputeBuffer frogMailBuffer;
	ComputeBuffer pixelsBuffer;
	ComputeBuffer pixelMailBuffer;


	protected virtual void Start()
	{
		Init();
		transform.GetComponentInChildren<MeshRenderer>().material.mainTexture = displayTexture;
	}


	void Init()
	{

		// Create render textures
		ComputeHelper.CreateRenderTexture(ref trailMap, settings.width, settings.height, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref diffusedTrailMap, settings.width, settings.height, filterMode, format);
		ComputeHelper.CreateRenderTexture(ref displayTexture, settings.width, settings.height, filterMode, format);

		// Create agents with initial positions and angles
		Agent[] agents = new Agent[settings.numAgents];
		for (int i = 0; i < agents.Length; i++)
		{
			Vector2 centre = new Vector2(settings.width / 2, settings.height / 2);
			Vector2 startPos = Vector2.zero;
			float randomAngle = Random.value * Mathf.PI * 2;
			float angle = 0;

			if (settings.spawnMode == SpawnMode.Point)
			{
				startPos = centre;
				angle = randomAngle;
			}
			else if (settings.spawnMode == SpawnMode.Random)
			{
				startPos = new Vector2(Random.Range(0, settings.width), Random.Range(0, settings.height));
				angle = randomAngle;
			}
			else if (settings.spawnMode == SpawnMode.InwardCircle)
			{
				startPos = centre + Random.insideUnitCircle * settings.height * 0.5f;
				angle = Mathf.Atan2((centre - startPos).normalized.y, (centre - startPos).normalized.x);
			}
			else if (settings.spawnMode == SpawnMode.RandomCircle)
			{
				startPos = centre + Random.insideUnitCircle * settings.height * 0.15f;
				angle = randomAngle;
			}

			Vector3Int speciesMask;
			int speciesIndex = 0;
			int numSpecies = settings.speciesSettings.Length;

			if (numSpecies == 1)
			{
				speciesMask = Vector3Int.one;
			}
			else
			{
				int species = Random.Range(1, numSpecies + 1);
				speciesIndex = species - 1;
				speciesMask = new Vector3Int((species == 1) ? 1 : 0, (species == 2) ? 1 : 0, (species == 3) ? 1 : 0);
			}



			agents[i] = new Agent() { position = startPos, angle = angle, speciesMask = speciesMask, speciesIndex = speciesIndex };
		}

		ComputeHelper.CreateAndSetBuffer<Agent>(ref agentBuffer, agents, compute, "agents", updateKernel);
		compute.SetInt("numAgents", settings.numAgents);
		drawAgentsCS.SetBuffer(0, "agents", agentBuffer);
		drawAgentsCS.SetInt("numAgents", settings.numAgents);


		compute.SetInt("width", settings.width);
		compute.SetInt("height", settings.height);


		int numPixels = settings.width * settings.height;

		ComputeHelper.CreateStructuredBuffer<Frog>(ref frogsBuffer, numFrogs);
		ComputeHelper.CreateStructuredBuffer<FrogMail>(ref frogMailBuffer, numFrogs);
		ComputeHelper.CreateStructuredBuffer<Pixel>(ref pixelsBuffer, numPixels);
		ComputeHelper.CreateStructuredBuffer<PixelMail>(ref pixelMailBuffer, numPixels);

		// PixelInit
		myCS.SetInt("width", settings.width);
		myCS.SetInt("height", settings.height);
		myCS.SetBuffer(PixelInit, "pixels", pixelsBuffer);
		myCS.SetBuffer(PixelInit, "pixelMail", pixelMailBuffer);
		ComputeHelper.Dispatch(myCS, settings.width, settings.height, 1, PixelInit);

		// FrogInit
		myCS.SetInt("numFrogs", numFrogs);
		myCS.SetBuffer(FrogInit, "frogs", frogsBuffer);
		myCS.SetBuffer(FrogInit, "pixels", pixelsBuffer);
		ComputeHelper.Dispatch(myCS, numFrogs, 1, 1, FrogInit);	

		// FrogAct
		myCS.SetBuffer(FrogAct, "frogs", frogsBuffer);
		myCS.SetBuffer(FrogAct, "frogMail", frogMailBuffer);
		myCS.SetBuffer(FrogAct, "pixels", pixelsBuffer);
		myCS.SetBuffer(FrogAct, "pixelMail", pixelMailBuffer);

		// FrogResolve
		myCS.SetBuffer(FrogResolve, "frogs", frogsBuffer);
		myCS.SetBuffer(FrogResolve, "frogMail", frogMailBuffer);

		// PixelResolve
		myCS.SetInt("width", settings.width);
		myCS.SetInt("height", settings.height);
		myCS.SetBuffer(PixelResolve, "pixels", pixelsBuffer);
		myCS.SetBuffer(PixelResolve, "pixelMail", pixelMailBuffer);
		myCS.SetBuffer(PixelResolve, "frogs", frogsBuffer);

		// FrogDisplay
		myCS.SetBuffer(FrogDisplay, "frogs", frogsBuffer);
		myCS.SetTexture(FrogDisplay, "display", displayTexture);

		// PixelDisplay
		myCS.SetBuffer(PixelDisplay, "pixels", pixelsBuffer);
		myCS.SetTexture(PixelDisplay, "display", displayTexture);

	}

	void FixedUpdate()
	{
		for (int i = 0; i < settings.stepsPerFrame; i++)
		{
			RunSimulation();
		}
	}

	void LateUpdate()
	{
		// if (showAgentsOnly)
		// {
		// 	ComputeHelper.ClearRenderTexture(displayTexture);

		// 	drawAgentsCS.SetTexture(0, "TargetTexture", displayTexture);
		// 	ComputeHelper.Dispatch(drawAgentsCS, settings.numAgents, 1, 1, 0);

		// }
		// else
		// {
		// 	ComputeHelper.CopyRenderTexture(trailMap, displayTexture);
		// }

		// PixelDisplay
		ComputeHelper.Dispatch(myCS, settings.width, settings.height, 1, PixelDisplay);

		// FrogDisplay
		ComputeHelper.Dispatch(myCS, numFrogs, 1, 1, FrogDisplay);
	}

	void RunSimulation()
	{

		var speciesSettings = settings.speciesSettings;
		ComputeHelper.CreateStructuredBuffer(ref settingsBuffer, speciesSettings);
		compute.SetBuffer(0, "speciesSettings", settingsBuffer);


		// Assign textures
		compute.SetTexture(updateKernel, "TrailMap", trailMap);
		compute.SetTexture(updateKernel, "DiffusedTrailMap", diffusedTrailMap);

		compute.SetTexture(diffuseMapKernel, "TrailMap", trailMap);
		compute.SetTexture(diffuseMapKernel, "DiffusedTrailMap", diffusedTrailMap);

		// Assign settings
		compute.SetFloat("deltaTime", Time.fixedDeltaTime);
		compute.SetFloat("time", Time.fixedTime);

		compute.SetFloat("trailWeight", settings.trailWeight);
		compute.SetFloat("decayRate", settings.decayRate);
		compute.SetFloat("diffuseRate", settings.diffuseRate);


		// ComputeHelper.Dispatch(compute, settings.numAgents, 1, 1, kernelIndex: updateKernel);
		// ComputeHelper.Dispatch(compute, settings.width, settings.height, 1, kernelIndex: diffuseMapKernel);

		// ComputeHelper.CopyRenderTexture(diffusedTrailMap, trailMap);




		// FrogAct
		myCS.SetFloat("time", Time.fixedTime);
		ComputeHelper.Dispatch(myCS, numFrogs, 1, 1, FrogAct);

		// FrogResolve
		ComputeHelper.Dispatch(myCS, numFrogs, 1, 1, FrogResolve);

		// PixelResolve
		ComputeHelper.Dispatch(myCS, settings.width, settings.height, 1, PixelResolve);

	}

	void OnDestroy()
	{
		// TODO: more
		ComputeHelper.Release(agentBuffer, settingsBuffer);
		ComputeHelper.Release(frogsBuffer, frogMailBuffer, pixelsBuffer, pixelMailBuffer);
	}

	public struct Agent
	{
		public Vector2 position;
		public float angle;
		public Vector3Int speciesMask;
		int unusedSpeciesChannel;
		public int speciesIndex;
	}

	struct FrogData {
		// USER DEFINED
		uint state;
	}
	struct Frog {
		Vector2Int position;
		FrogData data;
	}
	struct FrogMail {
		// USER DEFINED
		bool tag;
		Vector2Int position;
	}

	struct Pixel {
		int frogId;
		bool collision;
	};
	struct PixelMail {
		int frogId;
		int count;
	};

}
