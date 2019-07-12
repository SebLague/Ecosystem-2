using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamA : MonoBehaviour {

    public Vector3 targetPos;
    Vector3 startPos;
    public float easeTime;
    bool running;
    float t;

    void Start () {
        startPos = transform.position;
    }

    void Update () {
        if (Input.GetKeyDown (KeyCode.Space)) {
            running = true;
        }
        if (running) {
            t += Time.deltaTime * 1 / easeTime;
            float e = CubicEase (Mathf.Clamp01 (t));
            transform.position = Vector3.Lerp (startPos, targetPos, e);
        }
    }

    float CubicEase (float t, float b = 0, float c = 1, float d = 1) {
        t /= d / 2;
        if (t < 1) return c / 2 * t * t * t + b;
        t -= 2;
        return c / 2 * (t * t * t + 2) + b;
    }

    float Ease (float time) {
        time /= 1f/2;
        if (time < 1) {
            return 1 / 2f * time * time;
        }

        time--;
        return -1f / 2 * (time * (time - 2) - 1);
    }
}