using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lassu.Museum
{
    public enum MuseumDisplayStyle
    {
        Pedestal,
        Vitrine,
        WallPanel
    }

    [Serializable]
    public sealed class MuseumExhibitData
    {
        [SerializeField] private string title = "Item do acervo";
        [SerializeField, TextArea(2, 5)] private string description = "Modelo 3D pendente.";
        [SerializeField] private MuseumDisplayStyle displayStyle = MuseumDisplayStyle.Pedestal;
        [SerializeField] private GameObject modelPrefab;

        public string Title => title;
        public string Description => description;
        public MuseumDisplayStyle DisplayStyle => displayStyle;
        public GameObject ModelPrefab => modelPrefab;

        public MuseumExhibitData(string title, string description, MuseumDisplayStyle displayStyle)
        {
            this.title = title;
            this.description = description;
            this.displayStyle = displayStyle;
        }
    }

    [Serializable]
    public sealed class MuseumRoomData
    {
        [SerializeField] private int decade;
        [SerializeField] private string title;
        [SerializeField, TextArea(3, 8)] private string curatorialText;
        [SerializeField] private Color accentColor = new Color(0.1f, 0.75f, 0.65f);
        [SerializeField] private List<MuseumExhibitData> exhibits = new List<MuseumExhibitData>();

        public int Decade => decade;
        public string Title => title;
        public string CuratorialText => curatorialText;
        public Color AccentColor => accentColor;
        public IReadOnlyList<MuseumExhibitData> Exhibits => exhibits;

        public MuseumRoomData(
            int decade,
            string title,
            string curatorialText,
            Color accentColor,
            IEnumerable<MuseumExhibitData> exhibits)
        {
            this.decade = decade;
            this.title = title;
            this.curatorialText = curatorialText;
            this.accentColor = accentColor;
            this.exhibits = new List<MuseumExhibitData>(exhibits);
        }
    }

    [CreateAssetMenu(fileName = "MuseumTourCatalog", menuName = "Museum LASSU/Tour Catalog")]
    public sealed class MuseumTourCatalog : ScriptableObject
    {
        [SerializeField] private List<MuseumRoomData> rooms = new List<MuseumRoomData>();

        public IReadOnlyList<MuseumRoomData> Rooms => rooms;

        public void SetRooms(IEnumerable<MuseumRoomData> roomData)
        {
            rooms = new List<MuseumRoomData>(roomData);
        }
    }
}
