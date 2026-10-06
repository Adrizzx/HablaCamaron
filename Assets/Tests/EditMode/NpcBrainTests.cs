using NUnit.Framework;
using UnityEngine;
using HablaCamaron.AI;

namespace HablaCamaron.Tests
{
    /// <summary>
    /// La FSM del conductor NPC como tabla de verdad: sensores → estado.
    /// Fija las PRIORIDADES (choque inminente > semáforo > seguir > crucero)
    /// y las personalidades (el taxista que decidió pasarse el rojo, pasa).
    /// </summary>
    public class NpcBrainTests
    {
        private DriverProfile _particular, _taxista;

        [SetUp]
        public void CreateProfiles()
        {
            _particular = DriverProfile.Particular();
            _taxista = DriverProfile.Taxista();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(_particular);
            Object.DestroyImmediate(_taxista);
        }

        private static NpcSensors Clear() =>
            new NpcSensors { AheadDistance = float.MaxValue };

        [Test]
        public void ViaLibre_Crucero()
        {
            var s = Clear();
            var d = NpcBrain.Decide(s, _particular, obeysThisLight: true);
            Assert.AreEqual(NpcState.Cruising, d.State);
            Assert.AreEqual(_particular.CruiseSpeed, d.TargetSpeed);
        }

        [Test]
        public void AlgoMuyCerca_FrenaEnSeco_AunqueElSemaforoEsteVerde()
        {
            var s = Clear();
            s.AheadDistance = 1f; // pegado
            var d = NpcBrain.Decide(s, _particular, true);
            Assert.AreEqual(NpcState.Braking, d.State);
            Assert.AreEqual(0f, d.TargetSpeed);
        }

        [Test]
        public void SemaforoEnRojoCerca_Frena()
        {
            var s = Clear();
            s.RedLightAhead = true;
            s.LightDistance = 8f;
            var d = NpcBrain.Decide(s, _particular, obeysThisLight: true);
            Assert.AreEqual(NpcState.Braking, d.State);
            Assert.Less(d.TargetSpeed, _particular.CruiseSpeed * 0.5f);
        }

        [Test]
        public void SemaforoEnRojoDentroDelMargenDeSeguridad_ObjetivoEsCeroEnSeco()
        {
            // Bug real cazado con SemaforosNpcDiagTests: el objetivo de
            // frenado original solo llegaba a 0 en el nodo EXACTO (asíntota),
            // así que el auto nunca terminaba de parar y cruzaba en rojo
            // reptando — hasta con perfiles 100% obedientes. Dentro de
            // StopMargin metros de la línea, el objetivo debe ser 0 en seco.
            var s = Clear();
            s.RedLightAhead = true;
            s.LightDistance = NpcBrain.StopMargin * 0.5f; // bien dentro del margen
            var d = NpcBrain.Decide(s, _particular, obeysThisLight: true);
            Assert.AreEqual(NpcState.Braking, d.State);
            Assert.AreEqual(0f, d.TargetSpeed, "dentro del margen de seguridad debe exigir PARE total");
        }

        [Test]
        public void PerfilMalConfigurado_LightBrakeDistanceIgualAlMargen_NoDaNaN()
        {
            // Revisión de código (ronda 1): un perfil con LightBrakeDistance
            // <= StopMargin dejaría la zona proporcional con ancho cero o
            // negativo — sin la guarda, división por cero (y hasta NaN si el
            // numerador también daba 0). El resultado esperado es el más
            // seguro posible: PARE total, nunca un TargetSpeed inválido.
            var p = ScriptableObject.CreateInstance<DriverProfile>();
            p.CruiseSpeed = 9f;
            p.LightBrakeDistance = NpcBrain.StopMargin; // igual al margen: caso límite exacto
            var s = Clear();
            s.RedLightAhead = true;
            s.LightDistance = NpcBrain.StopMargin - 0.01f; // dentro de la (diminuta) zona

            var d = NpcBrain.Decide(s, p, obeysThisLight: true);

            Assert.AreEqual(NpcState.Braking, d.State);
            Assert.IsFalse(float.IsNaN(d.TargetSpeed), "un perfil mal configurado no debe producir NaN");
            Assert.AreEqual(0f, d.TargetSpeed, "sin margen real de frenado, el resultado seguro es PARE total");
            Object.DestroyImmediate(p);
        }

        [Test]
        public void ElTaxistaQueDecidioPasarse_SigueDeLargo()
        {
            var s = Clear();
            s.RedLightAhead = true;
            s.LightDistance = 8f;
            var d = NpcBrain.Decide(s, _taxista, obeysThisLight: false);
            Assert.AreEqual(NpcState.Cruising, d.State, "El rojo era sugerencia");
        }

        [Test]
        public void VehiculoAdelante_LoSigueMasLento()
        {
            var s = Clear();
            s.AheadDistance = _particular.MinGap * 1.5f;
            var d = NpcBrain.Decide(s, _particular, true);
            Assert.AreEqual(NpcState.Following, d.State);
            Assert.Less(d.TargetSpeed, _particular.CruiseSpeed);
            Assert.Greater(d.TargetSpeed, 0f);
        }

        [Test]
        public void CedaConTrafico_SeDetiene()
        {
            var s = Clear();
            s.YieldAhead = true;
            var d = NpcBrain.Decide(s, _particular, true);
            Assert.AreEqual(NpcState.Yielding, d.State);
            Assert.AreEqual(0f, d.TargetSpeed);
        }

        [Test]
        public void ElJugadorEnLaBurbuja_ParadaTOTAL_NoGateo()
        {
            // La regla de oro del playtest: al que aprende JAMÁS se lo embiste.
            // El NPC se detiene del todo y espera (y pita) hasta que se quite.
            var s = Clear();
            s.PlayerNear = true;
            var d = NpcBrain.Decide(s, _taxista, true); // hasta el taxista apurado
            Assert.AreEqual(NpcState.Braking, d.State);
            Assert.AreEqual(0f, d.TargetSpeed, "cero absoluto: nada de gatear hacia el jugador");
        }

        [Test]
        public void ElJugadorEnLaBurbuja_Manda_SobreElSemaforoEnVerde()
        {
            // Aunque la vía esté libre y el semáforo en verde, con el jugador
            // adelante el NPC espera.
            var s = Clear();
            s.PlayerNear = true;
            s.RedLightAhead = false;
            var d = NpcBrain.Decide(s, _particular, true);
            Assert.AreEqual(0f, d.TargetSpeed);
        }

        [Test]
        public void PersonalidadesDelGdd_TaxistaApurado_BusetaLenta()
        {
            var buseta = DriverProfile.Buseta();
            Assert.Greater(_taxista.CruiseSpeed, _particular.CruiseSpeed, "taxista apurado");
            Assert.Less(_taxista.MinGap, _particular.MinGap, "taxista se pega");
            Assert.Less(_taxista.LightObedience, 1f, "el rojo no siempre lo frena");
            Assert.Greater(buseta.DoubleParkChance, 0f, "la buseta para donde sea");
            Object.DestroyImmediate(buseta);
        }
    }
}
