using NUnit.Framework;
using Pooling;
using UnityEngine;

namespace Paper2.Pooling.Editor.Tests
{
    [TestFixture]
    public class VariantPoolTests
    {
        private readonly GameObjectVariantPool _variantPool = new GameObjectVariantPool();

        private GameObject _container;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _container = new GameObject("Test container");
            _prefab = new GameObject("Test object");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_container);
            Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void ShouldGrowOnlyOnceOnMultipleGetCalls()
        {
            var config = new VariantPoolConfig
            {
                Variant = "Test",
                Prefab = _prefab,
                Increment = 13,
                GrowCap = 99,
                PoolCap = 99
            };

            _variantPool.Init(config, _container.transform);

            var item1 = _variantPool.Get();
            var item2 = _variantPool.Get();

            Assert.AreEqual(2, _variantPool.ActiveItems.Count);
            Assert.AreEqual(12, _variantPool.InactiveItems.Count);

            _variantPool.Return(item2);
            _variantPool.Return(item1);

            Assert.AreEqual(0, _variantPool.ActiveItems.Count);
            Assert.AreEqual(14, _variantPool.InactiveItems.Count);
        }
    }
}
