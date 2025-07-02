using Sim.Util;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sim.Cloth
{
	public class ClothGPU : MonoBehaviour
	{
		const int THREAD_SIZE = 16;

		[Header("Compute Cloth")]
		public ComputeShader clothCompute;
		public Material clothMaterial;
		public Transform box;
		public int resolutionX = 64;
		public int resolutionY = 64;
		public float spacing = 0.2f;
		public float stiffness = 0.5f;
		public float damping = 0.99f;
		public float gravity = -9.8f;
		public float deltaTime = 0.05f;

		[Header("Box Params")]
		public Vector3 startPos;
		public Vector3 endPos;
		public float speed;

		/* Buffers */
		private ComputeBuffer positionBufferRead;
		private ComputeBuffer prevPositionBufferRead;
		private ComputeBuffer positionBufferWrite;
		private ComputeBuffer prevPositionBufferWrite;
		private ComputeBuffer indicesBuffer;
		private ComputeBuffer normalBuffer;
		private ComputeBuffer intNormalBuffer;
		private ComputeBuffer accelBuffer;
		private ComputeBuffer isFixedBuffer;
		
		/* Arrays */
		private Vector3[] positionArray;
		private Vector3[] prevPositionArray;
		private Vector3[] normalArray;
		private Vector3[] accelArray;
		private Vector2[] uvArray;
		private int[] indexArray;
		private int[] isFixedArray;

		/* Kernel IDs */
		private int externalForceKernel		= 0;
		private int computeParticlesKernel	= 1;
		private int constraintKernel		= 2;
		private int collisionKernel			= 3;
		private int generateMeshKernel		= 4;
		private int computeNormalKernel		= 5;
		private int normNormalKernel		= 6;

		private void Start()
		{
			InitArray();
			InitBuffer();
			InitShader();
		}

		private void Update()
		{
			UpdateBoxPosition();
			UpdateParams();
			DispatchShader();
			DrawClothMesh(positionArray, indexArray);
		}

		private void InitArray()
		{
			indexArray = new int[(resolutionX - 1) * (resolutionY - 1) * 6];
			normalArray = new Vector3[resolutionX * resolutionY];
			positionArray = new Vector3[resolutionX * resolutionY];
			prevPositionArray = new Vector3[resolutionX * resolutionY];
			accelArray = new Vector3[resolutionX * resolutionY];
			isFixedArray = new int[resolutionX * resolutionY];
			uvArray = new Vector2[resolutionX * resolutionY];

			for (int i = 0; i < resolutionX; i++)
				for(int j=0; j<resolutionY; j++)
				{
					positionArray[i + j * resolutionX] = new Vector3(i * spacing - resolutionX * spacing / 2, j * spacing - resolutionY * spacing / 2, 0);
					prevPositionArray[i + j * resolutionX] = positionArray[i + j * resolutionX];
					isFixedArray[i + j * resolutionX] = (j == (resolutionY - 1)) ? 1 : 0;
					uvArray[i + j * resolutionX] = new Vector2((float)i / resolutionX, (float)j / resolutionY);
				}
		}

		private void InitBuffer()
		{
			positionBufferRead = new ComputeBuffer(resolutionX * resolutionY, sizeof(float) * 3);
			prevPositionBufferRead = new ComputeBuffer(resolutionX * resolutionY, sizeof(float) * 3);
			positionBufferWrite = new ComputeBuffer(resolutionX * resolutionY, sizeof(float) * 3);
			prevPositionBufferWrite = new ComputeBuffer(resolutionX * resolutionY, sizeof(float) * 3);
			accelBuffer = new ComputeBuffer(resolutionX * resolutionY, sizeof(float) * 3);
			isFixedBuffer = new ComputeBuffer(resolutionX * resolutionY, sizeof(int));
			indicesBuffer = new ComputeBuffer((resolutionX - 1) * (resolutionY - 1) * 6, sizeof(int));
			normalBuffer = new ComputeBuffer(resolutionX * resolutionY, sizeof(float) * 3);
			intNormalBuffer = new ComputeBuffer(resolutionX * resolutionY, sizeof(int) * 3);

			accelBuffer.SetData(accelArray);
			isFixedBuffer.SetData(isFixedArray);
		}

		private void InitShader()
		{
			/* Set Material shader Params */
			Texture2D gradientTex = TextureGenerator.GenerateGradientTexture();
			clothMaterial.SetTexture("_GradientTex", gradientTex);
			clothMaterial.SetInt("_Resolution", resolutionX);

			/* Find kernels */
			externalForceKernel = clothCompute.FindKernel("ExternalForces");
			computeParticlesKernel = clothCompute.FindKernel("ComputeParticles");
			constraintKernel = clothCompute.FindKernel("ConstraintCorrection");
			collisionKernel = clothCompute.FindKernel("CollisionCheck");
			generateMeshKernel = clothCompute.FindKernel("GenerateMesh");
			computeNormalKernel = clothCompute.FindKernel("ComputeNormal");
			normNormalKernel = clothCompute.FindKernel("NormalizeNormal");

			/* Set Compute shader Params */
			clothCompute.SetInt("resolutionX", resolutionX);
			clothCompute.SetInt("resolutionY", resolutionY);
			clothCompute.SetFloat("spacing", spacing);

			clothCompute.SetBuffer(externalForceKernel, "accelBuffer", accelBuffer);

			clothCompute.SetBuffer(computeParticlesKernel, "positionBufferRead", positionBufferRead);
			clothCompute.SetBuffer(computeParticlesKernel, "positionBufferWrite", positionBufferWrite);
			clothCompute.SetBuffer(computeParticlesKernel, "prevPositionBufferRead", prevPositionBufferRead);
			clothCompute.SetBuffer(computeParticlesKernel, "prevPositionBufferWrite", prevPositionBufferWrite);
			clothCompute.SetBuffer(computeParticlesKernel, "accelBuffer", accelBuffer);
			clothCompute.SetBuffer(computeParticlesKernel, "isFixedBuffer", isFixedBuffer);

			clothCompute.SetBuffer(constraintKernel, "isFixedBuffer", isFixedBuffer);
			clothCompute.SetBuffer(constraintKernel, "positionBufferWrite", positionBufferWrite);

			clothCompute.SetBuffer(collisionKernel, "positionBufferWrite", positionBufferWrite);

			clothCompute.SetBuffer(generateMeshKernel, "positionBufferRead", positionBufferRead);
			clothCompute.SetBuffer(generateMeshKernel, "indicesBuffer", indicesBuffer);
			clothCompute.SetBuffer(generateMeshKernel, "intNormalBuffer", intNormalBuffer);

			clothCompute.SetBuffer(computeNormalKernel, "positionBufferRead", positionBufferRead);
			clothCompute.SetBuffer(computeNormalKernel, "intNormalBuffer", intNormalBuffer);

			clothCompute.SetBuffer(normNormalKernel, "normalBuffer", normalBuffer);
			clothCompute.SetBuffer(normNormalKernel, "intNormalBuffer", intNormalBuffer);
		}

		private bool reachedEnd = false;
		private float elapsedTime = 0;
		private void UpdateBoxPosition()
		{
			if (reachedEnd || box == null)
				return;

			float totalDist = Vector3.Distance(startPos, endPos);
			elapsedTime += (speed / totalDist) * deltaTime;
			elapsedTime = Mathf.Clamp01(elapsedTime);

			box.position = Vector3.Lerp(startPos, endPos, elapsedTime);

			if (elapsedTime >= 1f)
				reachedEnd = true;
		}

		private void UpdateParams()
		{
			/* Update Compute shader Params */
			clothCompute.SetFloat("stiffness", stiffness);
			clothCompute.SetFloat("damping", damping);
			clothCompute.SetFloat("gravity", gravity);
			clothCompute.SetFloat("deltaTime", deltaTime);
			clothCompute.SetFloat("time", Time.time);

			Vector3 boxMin = box.position - box.localScale * 0.6f;
			Vector3 boxMax = box.position + box.localScale * 0.6f;
			clothCompute.SetFloats("boxMin", new float[] { boxMin.x, boxMin.y, boxMin.z });
			clothCompute.SetFloats("boxMax", new float[] { boxMax.x, boxMax.y, boxMax.z });
		}

		private void DispatchShader()
		{
			/* Set Datas */
			positionBufferRead.SetData(positionArray);
			prevPositionBufferRead.SetData(prevPositionArray);

			/* Dispatch All */
			clothCompute.Dispatch(externalForceKernel, resolutionX / THREAD_SIZE, resolutionY / THREAD_SIZE, 1);
			clothCompute.Dispatch(computeParticlesKernel, resolutionX / THREAD_SIZE, resolutionY / THREAD_SIZE, 1);
			for (int t = 0; t < 5; t++)
			{
				clothCompute.Dispatch(collisionKernel, resolutionX / THREAD_SIZE, resolutionY / THREAD_SIZE, 1);
				clothCompute.Dispatch(constraintKernel, resolutionX / THREAD_SIZE, resolutionY / THREAD_SIZE, 1);
			}
			clothCompute.Dispatch(generateMeshKernel, resolutionX / THREAD_SIZE, resolutionX / THREAD_SIZE, 1);
			clothCompute.Dispatch(computeNormalKernel, resolutionX / THREAD_SIZE, resolutionY / THREAD_SIZE, 1);
			clothCompute.Dispatch(normNormalKernel, resolutionX / THREAD_SIZE, resolutionY / THREAD_SIZE, 1);

			/* Get Datas */
			positionBufferWrite.GetData(positionArray);
			prevPositionBufferWrite.GetData(prevPositionArray);
			indicesBuffer.GetData(indexArray);
			normalBuffer.GetData(normalArray);
		}

		private void DrawClothMesh(Vector3[] vertices, int[] indices)
		{
			Mesh clothMesh = new Mesh();
			clothMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			clothMesh.vertices = vertices;
			clothMesh.triangles = indices;
			clothMesh.normals = normalArray;
			clothMesh.uv = uvArray;

			Graphics.DrawMesh(clothMesh, Matrix4x4.identity, clothMaterial, 0);
		}

		private void OnDestroy()
		{
			/* Dispose All Buffers */
			positionBufferRead?.Dispose();
			prevPositionBufferRead?.Dispose();
			positionBufferWrite?.Dispose();
			prevPositionBufferWrite?.Dispose();
			indicesBuffer?.Dispose();
			normalBuffer?.Dispose();
			intNormalBuffer?.Dispose();
			isFixedBuffer?.Dispose();
			accelBuffer?.Dispose();
		}
	}
}