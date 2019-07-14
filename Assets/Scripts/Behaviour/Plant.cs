using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plant : LivingEntity {
    float amountRemaining = 1;

    public float Consume (float amount) {
        float amountConsumed = Mathf.Max (0, Mathf.Min (amountRemaining, amount));
        amountRemaining -= amount;

        transform.localScale = Vector3.one * amountRemaining;

        if (amountRemaining <= 0) {
            Environment.RegisterPlantDeath (this);
        }

        return amountConsumed;
    }
}