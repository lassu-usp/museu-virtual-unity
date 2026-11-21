using System.Collections.Generic;
using MuseuVirtual;
using UnityEngine;

namespace Lassu.Museum
{
    [DisallowMultipleComponent]
    public sealed class MuseumRoom : MonoBehaviour
    {
        [SerializeField] private int decade;
        [SerializeField] private string roomTitle = "Hall de entrada";
        [SerializeField, Min(0)] private int roomNumber;
        [SerializeField, TextArea(3, 8)] private string curatorialText;
        [SerializeField] private Color accentColor = new Color(0.1f, 0.75f, 0.65f);
        [SerializeField] private BoxCollider roomVolume;
        [SerializeField] private MuseumRoom nextRoom;
        [SerializeField] private List<MuseumExhibitDisplay> exhibits = new List<MuseumExhibitDisplay>();

        public int Decade => decade;
        public string RoomTitle => roomTitle;
        public int RoomNumber => roomNumber;
        public string CuratorialText => curatorialText;
        public Color AccentColor => accentColor;
        public MuseumRoom NextRoom => nextRoom;
        public IReadOnlyList<MuseumExhibitDisplay> Exhibits => exhibits;
        public string DisplayName => decade > 0 ? $"DÉCADA DE {decade}" : roomTitle.ToUpperInvariant();

        public void Configure(int newDecade, string newTitle, Color newAccent, BoxCollider volume)
        {
            decade = newDecade;
            roomTitle = newTitle;
            accentColor = newAccent;
            roomVolume = volume;
            EnsureTriggerVolume();
        }

        public void SetNextRoom(MuseumRoom room)
        {
            nextRoom = room;
        }

        public void SetCuratorialContent(int number, string text)
        {
            roomNumber = Mathf.Max(0, number);
            curatorialText = text ?? string.Empty;
        }

        public bool ContainsPosition(Vector3 worldPosition)
        {
            if (roomVolume == null || !roomVolume.enabled || !gameObject.activeInHierarchy) return false;
            var position = roomVolume.transform.InverseTransformPoint(worldPosition) - roomVolume.center;
            var halfSize = roomVolume.size * 0.5f;
            return Mathf.Abs(position.x) <= halfSize.x && Mathf.Abs(position.y) <= halfSize.y &&
                   Mathf.Abs(position.z) <= halfSize.z;
        }

        public void SetExhibits(IEnumerable<MuseumExhibitDisplay> roomExhibits)
        {
            exhibits = new List<MuseumExhibitDisplay>(roomExhibits);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<MuseumVisitor>() == null)
            {
                return;
            }

            MuseumHUD.Instance?.ShowRoom(this);
        }

        private void OnValidate()
        {
            EnsureTriggerVolume();
        }

        private void EnsureTriggerVolume()
        {
            if (roomVolume != null)
            {
                roomVolume.isTrigger = true;
            }
        }
    }
}
