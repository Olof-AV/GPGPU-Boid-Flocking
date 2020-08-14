using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Boid
{
    public Vector3 pos;
    public Vector3 dir;
}

public struct Enemy
{
    public Vector3 pos;
    public float radius;
}

public class Flock : MonoBehaviour
{
    //Shader stuff
    [SerializeField] private ComputeShader myShader = null;
    private int kernelHandle = 0;
    private uint workGroupSizeX = 0;
    private ComputeBuffer cBufferBoid = null;
    private ComputeBuffer cBufferEnemy = null;
    private ComputeBuffer bufferWithArgs = null;
    [SerializeField] private bool isPaused = false;

    //Parameters for drawing one boid
    [SerializeField] private Mesh boidMesh = null;
    [SerializeField] private Material boidMaterial = null;

    //Parameters for creating the flock
    [SerializeField] private int boidCount = 100;
    [SerializeField] private float spawnRadius = 3.0f;
    //[SerializeField] private GameObject boidTemplate = null; <- used in my first implementation with no GPU instancing

    //Parameters for controlling the flock
    [SerializeField] float neighbourRadius = 0.7f;

    [SerializeField] float turnSpeed = 0.5f;
    [SerializeField] float velocity = 5.0f;

    [SerializeField] float weightAlignment = 1f;
    [SerializeField] float weightCohesion = 1f;
    [SerializeField] float weightSeparation = 1f;
    [SerializeField] float weightSeek = 1f;

    //Avoidance
    [SerializeField] float avoidanceMultiplier = 5.0f;

    //Collections
    private Boid[] birdsData;
    //private GameObject[] birdObjects; <- used in my first implementation with no GPU instancing
    private EnemyScript[] enemies;
    private Enemy[] enemiesData;

    // Start is called before the first frame update
    void Start()
    {
        //Should be valid ofc
        if (myShader && boidMesh)
        {
            //Find shader
            kernelHandle = myShader.FindKernel("DoFlock");

            //Get work group size
            uint ignored = 0;
            myShader.GetKernelThreadGroupSizes(kernelHandle, out workGroupSizeX, out ignored, out ignored);

            //Create collections
            birdsData = new Boid[boidCount];
            //birdObjects = new GameObject[boidCount]; <- used in my first implementation with no GPU instancing
            enemies = FindObjectsOfType<EnemyScript>();
            enemiesData = new Enemy[enemies.Length];

            //Boids
            {
                //Initialise boid data
                for (int i = 0; i < boidCount; ++i)
                {
                    //Pos
                    birdsData[i].pos = transform.position + UnityEngine.Random.insideUnitSphere * spawnRadius;

                    //Direction is based off of pos, compared to spawn point
                    Vector3 offset = (birdsData[i].pos - transform.position);
                    offset.y = 0.0f;
                    offset.Normalize();

                    birdsData[i].dir = Quaternion.AngleAxis(-90.0f, Vector3.up) * offset;

                    //Instantiate objects from that data
                    //birdObjects[i] = Instantiate(boidTemplate, birdsData[i].pos, Quaternion.LookRotation(birdsData[i].dir)); <- used in my first implementation with no GPU instancing
                }

                //Create CBUFFER of compute shader for boids
                cBufferBoid = new ComputeBuffer(boidCount, 24);

                //Set buffer data for boids
                cBufferBoid.SetData(birdsData);
            }

            //Enemies
            {
                //Initialise enemy data
                for(int i = 0; i < enemies.Length; ++i)
                {
                    //Copy data over
                    enemiesData[i].pos = enemies[i].transform.position;
                    enemiesData[i].radius = enemies[i].AvoidanceRadius;
                }

                //Create CBUFFER of compute shader for enemies
                cBufferEnemy = new ComputeBuffer(enemies.Length, 16);

                //Set buffer data for enemies
                cBufferEnemy.SetData(enemiesData);
            }

            //Initialise buffer with args for instanced drawing
            //https://docs.unity3d.com/ScriptReference/Graphics.DrawMeshInstancedIndirect.html <- More info
            bufferWithArgs = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            bufferWithArgs.SetData(new uint[5] { boidMesh.GetIndexCount(0), (uint)boidCount, boidMesh.GetIndexStart(0), boidMesh.GetBaseVertex(0), 0 });
            //For the thing above: Index count per instance, instance count, start index location, base vertex location, start instance location
        }
    }

    //Release anything left at this point
    void OnDestroy()
    {
        if (bufferWithArgs.IsValid()) { bufferWithArgs.Release(); }
        if(cBufferBoid.IsValid()) { cBufferBoid.Release(); }
        if(cBufferEnemy.IsValid()) { cBufferEnemy.Release(); }
    }

    //Update size of enemy
    private void FixedUpdate()
    {
        //Update data
        for(int i = 0; i < enemies.Length; ++i)
        {
            enemiesData[i].pos = enemies[i].transform.position;
            enemiesData[i].radius = enemies[i].AvoidanceRadius;
        }

        //Update CBUFFER
        cBufferEnemy.SetData(enemiesData);
    }

    // Update is called once per frame
    void Update()
    {
        //Should be valid ofc
        if (myShader)
        {
            //Don't update boids if paused, just draw them
            if(!isPaused)
            {
                //Set variables
                {
                    //Misc
                    myShader.SetFloat("deltaTime", Time.smoothDeltaTime);
                    myShader.SetInt("boidCount", birdsData.Length);

                    //Behaviour
                    myShader.SetVector("target", transform.position);
                    myShader.SetFloat("neighbourRadius", neighbourRadius);
                    myShader.SetFloat("turnSpeed", turnSpeed);
                    myShader.SetFloat("velocity", velocity);

                    //Weights
                    myShader.SetFloat("weightAlignment", weightAlignment);
                    myShader.SetFloat("weightCohesion", weightCohesion);
                    myShader.SetFloat("weightSeparation", weightSeparation);
                    myShader.SetFloat("weightSeek", weightSeek);

                    //Avoidance
                    myShader.SetInt("enemyCount", enemiesData.Length);
                    myShader.SetFloat("avoidanceMultiplier", avoidanceMultiplier);
                }

                //Set boid buffer inside shader
                myShader.SetBuffer(kernelHandle, "boidBuffer", cBufferBoid);

                //Set enemy buffer inside shader
                myShader.SetBuffer(kernelHandle, "enemyBuffer", cBufferEnemy);

                //Run shader
                myShader.Dispatch(kernelHandle, boidCount / (int)workGroupSizeX + 1, 1, 1);
                //The amount of thread groups on the X axis can be manipulated freely, but too big numbers seem to slow it down to a halt

                /*//Get data back <- used in my first implementation with no GPU instancing
                //cBuffer.GetData(birdsData);

                //Set it <- used in my first implementation with no GPU instancing
                for (int i = 0; i < boidCount; ++i)
                {
                    birdObjects[i].transform.position = birdsData[i].pos;
                    birdObjects[i].transform.rotation = Quaternion.LookRotation(birdsData[i].dir);
                }*/
            }

            //Boid material shader reads in data manipulated by the compute shader to then draw the boids
            boidMaterial.SetBuffer("boidBuffer", cBufferBoid);

            //This is used to draw the boid model in an instanced fashion, much much faster than anything else
            Graphics.DrawMeshInstancedIndirect(boidMesh, 0, boidMaterial,
                new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), bufferWithArgs);
            //Don't modify the bounds otherwise the instances move around too
        }
    }
}
