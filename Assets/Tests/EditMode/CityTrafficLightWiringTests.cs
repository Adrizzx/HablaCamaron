using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HablaCamaron.World;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// Diagnóstico de cableado semáforo↔grafo en las escenas de ciudad. Cada
    /// poste físico (TrafficLightController.Lamps) debe tener un nodo del
    /// grafo marcado como semáforo (RoadNode.TrafficLightGroup >= 0) CERCA, y
    /// viceversa: un lado descableado explica que el playtest vea NPCs
    /// cruzando en rojo (la FSM de NpcBrain ya obedece el semáforo desde la
    /// Fase 2 — si el nodo del cruce no está marcado, simplemente no hay nada
    /// que obedecer ahí). Abre las escenas con EditorSceneManager: no las
    /// guarda ni las deja dirty, y restaura la escena activa al terminar.
    /// </summary>
    public class CityTrafficLightWiringTests
    {
        private const float RadioMetros = 6f;

        private string _escenaPrevia;

        [SetUp]
        public void GuardarEscenaActiva()
        {
            _escenaPrevia = SceneManager.GetActiveScene().path;
        }

        [TearDown]
        public void RestaurarEscenaPrevia()
        {
            // No guardamos nada (OpenScene en modo Single descarta cambios sin
            // preguntar cuando se llama desde código): solo dejamos el editor
            // donde estaba antes del test.
            if (!string.IsNullOrEmpty(_escenaPrevia))
                EditorSceneManager.OpenScene(_escenaPrevia, OpenSceneMode.Single);
            else
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        [Test]
        public void SemaforosDeLaCiudad_QuedanCableadosAlGrafo()
        {
            VerificarCableado("Assets/Scenes/N1_CiudadToon.unity");
        }

        [Test]
        public void SemaforosDeLaCiudadCompleta_QuedanCableadosAlGrafo()
        {
            VerificarCableado("Assets/Scenes/N2_QuitoCiudad.unity");
        }

        private static void VerificarCableado(string escenaPath)
        {
            var escena = EditorSceneManager.OpenScene(escenaPath, OpenSceneMode.Single);
            Assert.IsTrue(escena.IsValid(), $"No se pudo abrir la escena {escenaPath}.");

            var controller = Object.FindFirstObjectByType<TrafficLightController>();
            Assert.IsNotNull(controller,
                $"{escenaPath}: no hay TrafficLightController en la escena.");

            var roadGraph = Object.FindFirstObjectByType<RoadGraph>();
            Assert.IsNotNull(roadGraph, $"{escenaPath}: no hay RoadGraph en la escena.");

            // Postes físicos: posición del ancla verde (o roja si la verde
            // faltara) de cada lámpara registrada en el controlador.
            var postes = new List<Vector3>();
            foreach (var lamp in controller.Lamps)
            {
                var anchor = lamp.GreenLight != null ? lamp.GreenLight : lamp.RedLight;
                if (anchor != null) postes.Add(anchor.transform.position);
            }

            // Nodos-semáforo del grafo: los que el builder marcó con un grupo.
            var nodosSemaforo = new List<RoadNode>();
            foreach (var n in roadGraph.Data.Nodes)
                if (n.TrafficLightGroup >= 0) nodosSemaforo.Add(n);

            var incumplimientos = new List<string>();

            // (a) todo poste debe tener un nodo-semáforo a ≤ RadioMetros.
            foreach (var poste in postes)
            {
                float mejor = MenorDistanciaAPuntos(poste, nodosSemaforo);
                if (mejor > RadioMetros)
                    incumplimientos.Add(
                        $"Poste HUÉRFANO en {poste} (nodo-semáforo más cercano a {mejor:F1} m)");
            }

            // (b) todo nodo-semáforo debe tener un poste a ≤ RadioMetros.
            foreach (var nodo in nodosSemaforo)
            {
                float mejor = MenorDistanciaAVectores(nodo.Position, postes);
                if (mejor > RadioMetros)
                    incumplimientos.Add(
                        $"Nodo FANTASMA {nodo.Id} en {nodo.Position} (poste más cercano a {mejor:F1} m)");
            }

            foreach (var linea in incumplimientos)
                Debug.LogError($"[Cableado semáforos] {escenaPath}: {linea}");

            Debug.Log($"[Cableado semáforos] {escenaPath}: {postes.Count} postes físicos, " +
                $"{nodosSemaforo.Count} nodos-semáforo del grafo, {incumplimientos.Count} incumplimiento(s).");

            Assert.IsEmpty(incumplimientos,
                $"{escenaPath}: {incumplimientos.Count} incumplimiento(s) de cableado semáforo↔grafo " +
                $"(radio {RadioMetros} m):\n" + string.Join("\n", incumplimientos));
        }

        private static float MenorDistanciaAVectores(Vector3 desde, List<Vector3> puntos)
        {
            float mejor = float.MaxValue;
            foreach (var p in puntos)
            {
                float d = Vector3.Distance(desde, p);
                if (d < mejor) mejor = d;
            }
            return mejor;
        }

        private static float MenorDistanciaAPuntos(Vector3 desde, List<RoadNode> nodos)
        {
            float mejor = float.MaxValue;
            foreach (var n in nodos)
            {
                float d = Vector3.Distance(desde, n.Position);
                if (d < mejor) mejor = d;
            }
            return mejor;
        }
    }
}
