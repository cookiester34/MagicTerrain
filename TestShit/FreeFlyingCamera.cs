using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeFlyingCamera : MonoBehaviour
{
    public float movementSpeed = 10.0f;
    public float mouseSensitivity = 2.0f;
    private bool mouseVisible = true;

    private bool frozen;
    
    void Start()
    {
        Cursor.visible = mouseVisible;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            frozen = !frozen;
        }
        if (frozen) return;
        
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        transform.position += transform.forward * vertical * movementSpeed * Time.deltaTime;
        transform.position += transform.right * horizontal * movementSpeed * Time.deltaTime;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        transform.Rotate(-mouseY * mouseSensitivity, mouseX * mouseSensitivity, 0);
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            mouseVisible = !mouseVisible;
            Cursor.visible = mouseVisible;
            if (mouseVisible)
            {
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.lockState = CursorLockMode.Confined;
            }
        }
    }
}
