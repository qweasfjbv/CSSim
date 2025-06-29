using Sim.Util;
using System.Collections.Generic;
using UnityEngine;

namespace Sim
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
		private int paramKernel = 2;

		private ComputeBuffer waveBuffer;
		private ComputeBuffer nextWaveBuffer;
		private ComputeBuffer matrixBuffer;
		private ComputeBuffer verticesBuffer;
		private ComputeBuffer triangleBuffer;

		private WavePoint[] wavePoints;
		private Vector3[] verticesArray;
		private int[] triangleArray;
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
			UpdateMaterialParamsGPU();
			GenerateMeshGPU();
			GenerateImpulse();
		}

		private void InitArrays()
		{
			matrices = new Matrix4x4[Count];

			wavePoints = new WavePoint[Count];
			verticesArray = new Vector3[Count];
			triangleArray = new int[(resolutionX - 1) * (resolutionZ - 1) * 6];
		}

		private void InitBuffers()
		{
			matrixBuffer = new ComputeBuffer(Count, sizeof(float) * 16);
			verticesBuffer = new ComputeBuffer(Count, sizeof(float) * 3);
			triangleBuffer = new ComputeBuffer((resolutionX - 1) * (resolutionZ - 1) * 6, sizeof(int));
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
			waveKernel = waveCompute.FindKernel("CSMain");
			meshKernel = waveCompute.FindKernel("GenerateMesh");
			paramKernel = waveCompute.FindKernel("ComputeMaterialParams");

			waveCompute.SetBuffer(waveKernel, "waveBuffer", waveBuffer);
			waveCompute.SetBuffer(waveKernel, "nextWaveBuffer", nextWaveBuffer);

			waveCompute.SetBuffer(meshKernel, "waveBuffer", waveBuffer);
			waveCompute.SetBuffer(meshKernel, "verticesBuffer", verticesBuffer);
			waveCompute.SetBuffer(meshKernel, "triangleBuffer", triangleBuffer);

			waveCompute.SetBuffer(paramKernel, "matrixBuffer", matrixBuffer);
			waveCompute.SetBuffer(paramKernel, "waveBuffer", waveBuffer);

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

		private void DispatchComputeShaders()
		{
			waveBuffer.SetData(wavePoints);

			waveCompute.Dispatch(waveKernel, resolutionX / 16, resolutionZ / 16, 1);
			waveCompute.Dispatch(meshKernel, resolutionX / 16, resolutionZ / 16, 1);
			waveCompute.Dispatch(paramKernel, resolutionX / 16, resolutionZ / 16, 1);

			nextWaveBuffer.GetData(wavePoints);
			verticesBuffer.GetData(verticesArray);
			triangleBuffer.GetData(triangleArray);
		}

		private void UpdateMaterialParamsGPU()
		{
			material.SetBuffer("matrixBuffer", matrixBuffer);
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
			waveMesh.RecalculateNormals();
			Graphics.DrawMesh(waveMesh, Matrix4x4.identity, material, 0);
		}

		private float elapsedTime = 0f;
		private void GenerateImpulse()
		{
			elapsedTime += fixedDeltaTime;
			if (elapsedTime < waveInterval) return;

			elapsedTime = 0f;
			int centerIdx = Random.Range(0, resolutionX) + Random.Range(0, resolutionZ) * resolutionX;
			wavePoints[centerIdx].velocity += impulseStrength;
			waveBuffer.SetData(wavePoints);
		}

		void OnDestroy()
		{
			waveBuffer?.Dispose();
			matrixBuffer?.Dispose();
			verticesBuffer?.Dispose();
			triangleBuffer?.Dispose();
			nextWaveBuffer?.Dispose();
		}
	}
}