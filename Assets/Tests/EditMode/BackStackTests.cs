using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SlimeColorMaker
{
    public class BackStackTests
    {
        readonly List<Action> pushed = new();
        readonly List<string> closed = new();

        // 실제 화면처럼 닫힐 때 스스로 스택에서 빠지는 닫기를 만든다.
        Action Closer(string name)
        {
            Action close = null;
            close = () =>
            {
                BackStack.Remove(close);
                closed.Add(name);
            };
            pushed.Add(close);
            return close;
        }

        // NUnit은 테스트마다 같은 인스턴스를 쓰므로 기록도 비운다.
        [TearDown]
        public void RemovePushed()
        {
            foreach (var close in pushed) BackStack.Remove(close);
            pushed.Clear();
            closed.Clear();
        }

        [Test]
        public void TryCloseTop_IsFalseWhenEmpty()
        {
            Assert.AreEqual(0, BackStack.Count);
            Assert.IsFalse(BackStack.TryCloseTop());
        }

        [Test]
        public void TryCloseTop_ClosesLastOpenedFirst()
        {
            BackStack.Push(Closer("popup"));
            BackStack.Push(Closer("confirm"));

            Assert.IsTrue(BackStack.TryCloseTop());
            Assert.IsTrue(BackStack.TryCloseTop());

            CollectionAssert.AreEqual(new[] { "confirm", "popup" }, closed);
        }

        [Test]
        public void Push_SameCloserAgain_MovesItToTop()
        {
            var first = Closer("first");
            BackStack.Push(first);
            BackStack.Push(Closer("second"));
            int count = BackStack.Count;

            BackStack.Push(first);

            Assert.AreEqual(count, BackStack.Count);
            BackStack.TryCloseTop();
            CollectionAssert.AreEqual(new[] { "first" }, closed);
        }

        [Test]
        public void Remove_TakesOutClosedScreen()
        {
            var popup = Closer("popup");
            BackStack.Push(Closer("pause"));
            BackStack.Push(popup);

            BackStack.Remove(popup);
            BackStack.TryCloseTop();

            CollectionAssert.AreEqual(new[] { "pause" }, closed);
        }
    }
}
