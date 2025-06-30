using Sim.Util;
using Unity.Mathematics;
using UnityEngine;

namespace Sim.Wave
{
	struct WavePoint
	{
		public float height;
		public float velocity;
	}

	public class WaveGPU : MonoBehaviour
	{
		public ComputeShader waveCompute;
		public Material material;
		public int resolutionX = 64;
		public int resolutionZ = 64;
		public float spacing = 0.5f;
		public float tension = 1.0f;
		public float damping = 0.995f;
		public float impulseStrength = 1.0f;
		public float wavelengthFactor = 1.0f;
		public float waveInterval = 0.15f;
		public float restoreStrength = 0.01f;
		public float fixedDeltaTime = 0.05f;

		private int waveKernel = 0;
		private int meshKernel = 1;
		private int computeNormalKernel = 2;
		private int normalKernel = 3;

		private ComputeBuffer waveBuffer;
		private ComputeBuffer nextWaveBuffer;
		private ComputeBuffer verticesBuffer;
		private ComputeBuffer triangleBuffer;
		private ComputeBuffer intNormalBuffer;
		private ComputeBuffer normalBuffer;

		private WavePoint[] wavePoints;
		private Vector3[] verticesArray;
		private int[] triangleArray;
		private int3[] intNormalArray;
		private Vector3[] normalArray; 
		private Matrix4x4[] matrices;

		public int Count { get => resolutionX * resolutionZ; }
		
		void Start()
		{
			InitArrays();
			InitBuffers();
			InitShaders();
		}

		void Update()
		{
			UpdateComputeShaderParams();
			DispatchComputeShaders();
			GenerateMeshGPU();
			GenerateImpulse();
		}

		private void InitArrays()
		{
			wavePoints = new WavePoint[Count];
			verticesArray = new Vector3[Count];
			triangleArray = new int[(resolutionX - 1) * (resolutionZ - 1) * 6];
			normalArray = new Vector3[Count];
			intNormalArray = new int3[Count];
		}

		private void InitBuffers()
		{
			verticesBuffer = new ComputeBuffer(Count, sizeof(float) * 3);
			triangleBuffer = new ComputeBuffer((resolutionX - 1) * (resolutionZ - 1) * 6, sizeof(int));
			normalBuffer = new ComputeBuffer(Count, sizeof (float) * 3);
			intNormalBuffer = new ComputeBuffer(Count, sizeof (int) * 3);
			waveBuffer = new ComputeBuffer(Count, sizeof(float) * 2);
			nextWaveBuffer = new ComputeBuffer(Count, sizeof(float) * 2);
		}

		private void InitShaders()
		{
			/** Material **/
			Texture2D gradientTex = TextureGenerator.GenerateGradientTexture();
			material.SetTexture("_GradientTex", gradientTex);
			material.SetFloat("_EdgeLength", resolutionX * spacing);

			/** Compute Shader **/
			waveKernel = waveCompute.FindKernel("ComputeWave");
			meshKernel = waveCompute.FindKernel("GenerateMesh");
			computeNormalKernel = waveCompute.FindKernel("ComputeNormal");
			normalKernel = waveCompute.FindKernel("NormalizeNormal");

			waveCompute.SetBuffer(waveKernel, "waveBuffer", waveBuffer);
			waveCompute.SetBuffer(waveKernel, "nextWaveBuffer", nextWaveBuffer);

			waveCompute.SetBuffer(meshKernel, "waveBuffer", waveBuffer);
			waveCompute.SetBuffer(meshKernel, "verticesBuffer", verticesBuffer);
			waveCompute.SetBuffer(meshKernel, "triangleBuffer", triangleBuffer);
			waveCompute.SetBuffer(meshKernel, "intNormalBuffer", intNormalBuffer);

			waveCompute.SetBuffer(computeNormalKernel, "verticesBuffer", verticesBuffer);
			waveCompute.SetBuffer(computeNormalKernel, "intNormalBuffer", intNormalBuffer);

			waveCompute.SetBuffer(normalKernel, "intNormalBuffer", intNormalBuffer);
			waveCompute.SetBuffer(normalKernel, "normalBuffer", normalBuffer);

			waveCompute.SetInt("resolutionX", resolutionX);
			waveCompute.SetInt("resolutionZ", resolutionZ);
			waveCompute.SetFloat("spacing", spacing);
		}

		private void UpdateComputeShaderParams()
		{
			waveCompute.SetFloat("deltaTime", fixedDeltaTime);
			waveCompute.SetFloat("tension", tension);
			waveCompute.SetFloat("damping", damping);
			waveCompute.SetFloat("wavelengthFactor", wavelengthFactor);
			waveCompute.SetFloat("restoreStrength", restoreStrength);
		}

		int numthread = 16;
		private void DispatchComputeShaders()
		{
			waveBuffer.SetData(wavePoints);
			intNormalBuffer.SetData(intNormalArray);

			waveCompute.Dispatch(waveKernel, resolutionX / numthread, resolutionZ / numthread, 1);
			waveCompute.Dispatch(meshKernel, resolutionX / numthread, resolutionZ / numthread, 1);
			waveCompute.Dispatch(computeNormalKernel, resolutionX / numthread, resolutionZ / numthread, 1);
			waveCompute.Dispatch(normalKernel, resolutionX / numthread, resolutionZ / numthread, 1);

			nextWaveBuffer.GetData(wavePoints);
			verticesBuffer.GetData(verticesArray);
			triangleBuffer.GetData(triangleArray);
			normalBuffer.GetData(normalArray);
		}

		private void GenerateMeshGPU()
		{
			DrawWaveMesh(verticesArray, triangleArray);
		}

		private void DrawWaveMesh(Vector3[] vertices, int[] indices)
		{
			Mesh waveMesh = new Mesh();
			waveMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			waveMesh.vertices = vertices;
			waveMesh.triangles = indices;
			waveMesh.normals = normalArray;

			Graphics.DrawMesh(waveMesh, Matrix4x4.identity, material, 0);
		}

		private float elapsedTime = 0f;
		private void GenerateImpulse()
		{
			elapsedTime += fixedDeltaTime;
			if (elapsedTime < waveInterval) return;

			elapsedTime = 0f;
			int centerIdx = UnityEngine.Random.Range(0, resolutionX) + UnityEngine.Random.Range(0, resolutionZ) * resolutionX;
			wavePoints[centerIdx].velocity += impulseStrength;
		}

		void OnDestroy()
		{
			waveBuffer?.Dispose();
			verticesBuffer?.Dispose();
			triangleBuffer?.Dispose();
			nextWaveBuffer?.Dispose();
			intNormalBuffer?.Dispose();
			normalBuffer?.Dispose();
		}
	}
}