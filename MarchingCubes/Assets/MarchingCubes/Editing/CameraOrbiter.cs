using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraOrbiter : MonoBehaviour
{
    bool is_moved;
    Vector3 last_mouse_pos;
    Vector3 camera_angles = Vector3.zero;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButtonDown(1)) 
        {
            is_moved = true;
            last_mouse_pos = Input.mousePosition;
        }
        if(Input.GetMouseButtonUp(1)) 
        {
            is_moved = false;
        }

        if (is_moved)
        {
            Vector3 mouse_speed = Input.mousePosition - last_mouse_pos;
            last_mouse_pos = Input.mousePosition;
            camera_angles.x -= mouse_speed.y;
            camera_angles.y += mouse_speed.x;

            if (camera_angles.x > 90)
            {
                camera_angles.x = 90;
            }
            else if (camera_angles.x < -90)
            {
                camera_angles.x = -90;
            }
            if (camera_angles.y > 180)
            {
                camera_angles.y -= 360;
            }
            if (camera_angles.y < -180)
            {
                camera_angles.y += 360;
            }

            transform.rotation = Quaternion.Euler(camera_angles);
        }
    }
}
