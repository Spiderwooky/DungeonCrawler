using UnityEngine;

// Caméra à la troisième personne : suit le joueur avec un décalage relatif à sa rotation
// et un lissage SmoothDamp (pas de mouvement de caméra brusque entre deux cases).
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform player;
 
    [SerializeField] private Vector3 offset       = new Vector3(0f, 10f, -7f);
    [SerializeField] private float   lookAtHeight = 1.5f;
    [SerializeField] private float   smoothSpeed  = 8f; // Vitesse de lissage (plus grand = plus réactif)
 
    private Vector3 currentVelocity; // Utilisé en interne par SmoothDamp
 
    private void LateUpdate()
    {
        if (player == null) return;
 
        // Position cible : offset appliqué dans le repère local du joueur (suit sa rotation)
        Vector3 targetPosition = player.position + player.rotation * offset;
 
        // Interpolation fluide vers la position cible
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            1f / smoothSpeed
        );
 
        // Toujours regarder légèrement au-dessus des pieds du joueur
        transform.LookAt(player.position + Vector3.up * lookAtHeight);
    }
}