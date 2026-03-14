using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bl4st.TimeScaleToolbar
{
    public class DemoMovement : MonoBehaviour
    {
        public float speed = 3f;
        private void Update()
        {
            float displacement = Time.time * speed;
            transform.localPosition = new Vector3(Mathf.Cos(displacement) * 3f, Mathf.Sin(displacement) * 3f, 0);
        }
    }

}

