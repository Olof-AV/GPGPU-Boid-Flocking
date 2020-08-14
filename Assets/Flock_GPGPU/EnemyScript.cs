using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyScript : MonoBehaviour
{
    [SerializeField] private float avoidanceRadius = 10.0f;
    public float AvoidanceRadius
    {
        get { return avoidanceRadius; }
    }

    // Update is called once per frame
    void Update()
    {
        transform.localScale = new Vector3(avoidanceRadius, avoidanceRadius, avoidanceRadius);
    }
}
