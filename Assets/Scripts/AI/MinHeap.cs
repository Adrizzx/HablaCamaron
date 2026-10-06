using System.Collections.Generic;

namespace HablaCamaron.AI
{
    /// <summary>
    /// Cola de prioridad mínima (heap binario). Push/Pop en O(log n).
    /// Sustituye la frontera lineal del A*: el perfil .NET de Unity no trae
    /// System.Collections.Generic.PriorityQueue, así que la implementamos.
    /// Pura y testeable en EditMode.
    /// </summary>
    public class MinHeap
    {
        private readonly List<(int id, float pri)> _h = new List<(int, float)>();

        public int Count => _h.Count;

        public void Clear() => _h.Clear();

        /// <summary>Inserta un id con su prioridad y sube hasta su sitio.</summary>
        public void Push(int id, float pri)
        {
            _h.Add((id, pri));
            int i = _h.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_h[parent].pri <= _h[i].pri) break;
                (_h[parent], _h[i]) = (_h[i], _h[parent]);
                i = parent;
            }
        }

        /// <summary>Saca el id de menor prioridad (llamar solo con Count > 0).</summary>
        public int Pop()
        {
            int top = _h[0].id;
            int last = _h.Count - 1;
            _h[0] = _h[last];
            _h.RemoveAt(last);

            int i = 0, n = _h.Count;
            while (true)
            {
                int l = 2 * i + 1, r = 2 * i + 2, small = i;
                if (l < n && _h[l].pri < _h[small].pri) small = l;
                if (r < n && _h[r].pri < _h[small].pri) small = r;
                if (small == i) break;
                (_h[small], _h[i]) = (_h[i], _h[small]);
                i = small;
            }
            return top;
        }
    }
}
