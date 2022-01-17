using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using TMPro;

#pragma warning disable IDE0090 // Use 'new(...)'
#pragma warning disable IDE1006 // Naming Styles

// Please note, this code is not up to my usual Standards. All the Find* and Get* functions should be replaced with serialized references.

public class Amphipod : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	private	enum Type
	{
		Amber,
		Bronze,
		Copper,
		Desert,
	}

	private struct TypeData
	{
		public	Color	Color;
		public	int		Energy;
		public	int		Coordinate;
	}
						const						string								ENERGY_TEXT	= "ENERGY: ";

	[SerializeField]	private						Type								_type;

						private						PointerEventData					_cachedDragEventData;
						private						PointerEventData					_cachedPreDragEventData;

						private						Dictionary<GameObject, int>			_dragGhostDistances;

						private	static				int									_energy;

						private	static	readonly	ReadOnlyDictionary<Type, TypeData>	TYPE_DATA
		= new ReadOnlyDictionary<Type, TypeData>
		(
			new Dictionary<Type, TypeData>
			{
				{ Type.Amber, new TypeData { Color = Color.red, Energy = 1, Coordinate = -3 } },
				{ Type.Bronze, new TypeData { Color = Color.green, Energy = 10, Coordinate = -1 } },
				{ Type.Copper, new TypeData { Color = Color.yellow, Energy = 100, Coordinate = 1 } },
				{ Type.Desert, new TypeData { Color = Color.blue, Energy = 1000, Coordinate = 3 } }
			}
		);

	private void Start()
	{
		GetComponentInChildren<TextMeshPro>().text = _type.ToString().Substring(0, 1);
		GetComponent<SpriteRenderer>().color = TYPE_DATA[_type].Color;
		FindObjectsOfType<TextMeshPro>().First(t => t.transform.parent == null).text = ENERGY_TEXT + _energy;
	}

	private IEnumerable<Amphipod> _amphipodsInHole
	{
		get
		{
			return FindObjectsOfType<Amphipod>().Where(a => Mathf.RoundToInt(a.transform.position.x) == TYPE_DATA[_type].Coordinate);
		}
	}

	private bool _inCorrectHole
	{
		get
		{
			return Mathf.RoundToInt(transform.position.x) == TYPE_DATA[_type].Coordinate;
		}
	}

	private bool _inHallway
	{
		get
		{
			return Mathf.RoundToInt(transform.position.y) == 2;
		}
	}

	private Vector2Int _holePosition
	{
		get
		{
			return new Vector2Int(TYPE_DATA[_type].Coordinate, _amphipodsInHole.Count() - 2);
		}
	}

	private bool HoleIsClear(out int energyRequired)
	{
		if (FloodSpace().TryGetValue(_holePosition, out energyRequired))
		{
			return _amphipodsInHole.All(a => a._type == _type);
		}
		return false;
	}

	private Dictionary<Vector2Int, int> FloodSpace()
	{
		return FloodSpace_Internal(Vector2Int.RoundToInt(transform.position), 0, new Dictionary<Vector2Int, int>(),
			FindObjectsOfType<SpriteRenderer>().Where(s => s.gameObject != gameObject)
				.Select(s => Vector2Int.RoundToInt(s.transform.position)).ToArray());
	}

	private Dictionary<Vector2Int, int> FloodSpace_Internal(Vector2Int currentPosition,
		int currentDistance,Dictionary<Vector2Int, int> distances, Vector2Int[] obstructions)
	{
		if (!distances.Keys.Concat(obstructions).Contains(currentPosition))
		{
			distances.Add(currentPosition, currentDistance);
			new List<Vector2Int>() { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }
				.ForEach(o => FloodSpace_Internal(currentPosition + o, currentDistance + 1, distances, obstructions));
		}
		return distances;
	}

	private void DestroyGhosts()
	{
		if (_dragGhostDistances != null)
		{
			_dragGhostDistances.Keys.ToList().ForEach(Destroy);
			_dragGhostDistances.Clear();
			_dragGhostDistances = null;
		}
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		bool holeIsClear = HoleIsClear(out _);
		if (_cachedPreDragEventData == null && _cachedDragEventData == null && Input.touchCount <= 1
			&& !(holeIsClear && _inCorrectHole) && !(_inHallway && !holeIsClear))
		{
			_cachedPreDragEventData = eventData;

			if (!_inCorrectHole && holeIsClear)
			{
				return;
			}

			_dragGhostDistances = new Dictionary<GameObject, int>();
			FloodSpace().ToList().ForEach(d =>
			{
				GameObject ghost = Instantiate(this).gameObject;
				Destroy(ghost.GetComponent<Amphipod>());
				Destroy(ghost.GetComponent<Collider2D>());
				ghost.GetComponent<SpriteRenderer>().SetAlpha(0.5f);
				ghost.GetComponentInChildren<TextMeshPro>().text = d.Value.ToString();
				ghost.GetComponentInChildren<TextMeshPro>().SetAlpha(0.5f);
				ghost.transform.position = (Vector3Int)d.Key;
				_dragGhostDistances.Add(ghost, d.Value);
			});
			_dragGhostDistances.Where(g => TYPE_DATA.Values.Select(d => d.Coordinate).Contains(Mathf.RoundToInt(g.Key.transform.position.x)))
				.ToList().ForEach(g => { _dragGhostDistances.Remove(g.Key); Destroy(g.Key); });
		}
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		if (_cachedPreDragEventData != null && eventData.pointerId == _cachedPreDragEventData.pointerId && _cachedDragEventData == null)
		{
			_cachedPreDragEventData = null;
			DestroyGhosts();
			if (!_inCorrectHole && HoleIsClear(out int energyRequired))
			{
				_energy += energyRequired * TYPE_DATA[_type].Energy;
				transform.position = (Vector3Int) _holePosition;
				FindObjectsOfType<TextMeshPro>().First(t => t.transform.parent == null).text = ENERGY_TEXT + _energy;
			}
		}
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (_dragGhostDistances != null && _cachedPreDragEventData != null && eventData.pointerId == _cachedPreDragEventData.pointerId)
		{
			_cachedPreDragEventData = null;
			if (_cachedDragEventData == null && Input.touchCount <= 1)
			{
				_cachedDragEventData = eventData;
			}
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (_cachedDragEventData != null && eventData.pointerId == _cachedDragEventData.pointerId)
		{
			_cachedDragEventData = eventData;

			GameObject place = _dragGhostDistances.Keys.FirstOrDefault(g => Vector2Int.RoundToInt(g.transform.position)
				== Vector2Int.RoundToInt(FindObjectOfType<Camera>().ScreenToWorldPoint(_cachedDragEventData.position)));
			if (place != null)
			{
				transform.position = (Vector3Int) Vector2Int.RoundToInt(place.transform.position);
				GetComponentInChildren<TextMeshPro>().SetAlpha(0.0f);
			}
		}
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (_cachedDragEventData != null && eventData.pointerId == _cachedDragEventData.pointerId)
		{
			_cachedDragEventData = null;
			GameObject dropGhost = _dragGhostDistances.Keys.FirstOrDefault(g =>
				Vector2Int.RoundToInt(g.transform.position) == Vector2Int.RoundToInt(transform.position));
			if (dropGhost != null)
			{
				_energy += int.Parse(dropGhost.GetComponentInChildren<TextMeshPro>().text) * TYPE_DATA[_type].Energy;
				FindObjectsOfType<TextMeshPro>().First(t => t.transform.parent == null).text = ENERGY_TEXT + _energy;
			}
			DestroyGhosts();
			GetComponentInChildren<TextMeshPro>().SetAlpha(1.0f);
		}
	}
}

public static class Helpers
{
	public static void SetAlpha(this SpriteRenderer sprite, float alpha)
	{
		Color color = sprite.color;
		color.a = alpha;
		sprite.color = color;
	}
	public static void SetAlpha(this TMP_Text text, float alpha)
	{
		Color color = text.color;
		color.a = alpha;
		text.color = color;
	}
}

#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore IDE0090 // Use 'new(...)'