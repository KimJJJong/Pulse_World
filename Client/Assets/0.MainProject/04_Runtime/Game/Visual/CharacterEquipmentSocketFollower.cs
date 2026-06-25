using UnityEngine;

namespace RhythmRPG.Visual
{
    [DisallowMultipleComponent]
    public class CharacterEquipmentSocketFollower : MonoBehaviour
    {
        private Transform _characterRoot;
        private Transform _socket;
        private Vector3 _bindSocketLocalPosition;
        private Quaternion _bindSocketLocalRotation = Quaternion.identity;
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation = Quaternion.identity;
        private Vector3 _baseLocalScale = Vector3.one;

        public void Bind(Transform characterRoot, Transform socket)
        {
            _characterRoot = characterRoot;
            _socket = socket;
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
            _baseLocalScale = transform.localScale;

            if (_characterRoot == null || _socket == null)
            {
                enabled = false;
                return;
            }

            _bindSocketLocalPosition = _characterRoot.InverseTransformPoint(_socket.position);
            _bindSocketLocalRotation = Quaternion.Inverse(_characterRoot.rotation) * _socket.rotation;
            enabled = true;
        }

        private void LateUpdate()
        {
            if (_characterRoot == null || _socket == null)
                return;

            var currentSocketLocalPosition = _characterRoot.InverseTransformPoint(_socket.position);
            var currentSocketLocalRotation = Quaternion.Inverse(_characterRoot.rotation) * _socket.rotation;
            // Keep the root-authored placement, then apply only the socket's animation delta.
            var deltaRotation = currentSocketLocalRotation * Quaternion.Inverse(_bindSocketLocalRotation);

            transform.localPosition = currentSocketLocalPosition + deltaRotation * (_baseLocalPosition - _bindSocketLocalPosition);
            transform.localRotation = deltaRotation * _baseLocalRotation;
            transform.localScale = _baseLocalScale;
        }
    }
}
