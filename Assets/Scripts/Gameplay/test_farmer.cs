using UnityEngine;

public class test_farmer : MonoBehaviour
{
    [SerializeField] Player Player;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.W))
        {
            Player.Move();
        }
    }
}
