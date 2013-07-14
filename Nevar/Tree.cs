﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace Nevar
{
	public unsafe class Tree
	{
		public int BranchPages;
		public int LeafPages;
		public int OverflowPages;
		public int Depth;
		public int PageCount;

		private readonly SliceComparer _cmp;

		public Page Root;

		private Tree(SliceComparer cmp, Page root)
		{
			_cmp = cmp;
			Root = root;
		}

		public static Tree CreateOrOpen(Transaction tx, int root, SliceComparer cmp)
		{
			if (root != -1)
			{
				return new Tree(cmp, tx.GetPage(root));
			}

			// need to create the root
			var newRootPage = NewPage(tx, PageFlags.Leaf, 1);
			var tree = new Tree(cmp, newRootPage) {Depth = 1};
			var cursor = tx.GetCursor(tree);
			cursor.RecordNewPage(newRootPage, 1);
			return tree;
		}

		public void Add(Transaction tx, Slice key, Stream value)
		{
			if (value == null) throw new ArgumentNullException("value");
			if (value.Length > int.MaxValue) throw new ArgumentException("Cannot add a value that is over 2GB in size", "value");

			var cusror = tx.GetCursor(this);
			
			var page = FindPageFor(tx, key, cusror);

			if (page.HasSpaceFor(key,value) == false)
			{
				SplitPage(tx, key, value, -1, cusror);
				DebugValidateTree(tx, cusror.Root);
				return;
			}

			page.AddNode(page.LastSearchPosition, key, value, 0);

			page.DebugValidate(_cmp);
		}

		private void DebugValidateTree(Transaction tx, Page root)
		{
			var stack = new Stack<Page>();
			stack.Push(root);
			while (stack.Count >0)
			{
				var p = stack.Pop();
				p.DebugValidate(_cmp);
				if (p.IsBranch == false)
					continue;
				for (int i = 0; i < p.NumberOfEntries; i++)
				{
					stack.Push(tx.GetPage(p.GetNode(i)->PageNumber));
				}
			}
		}

		private Page SplitPage(Transaction tx, Slice newKey, Stream value, int pageNumber, Cursor cursor)
		{
			Page parentPage;
			var page = cursor.Pop();
			var newPosition = true;
			var currentIndex = page.LastSearchPosition;

			var rightPage = NewPage(tx, page.Flags, 1);
			cursor.RecordNewPage(page, 1);
			rightPage.Flags = page.Flags;
			if (cursor.Pages.Count == 0) // we need to do a root split
			{
				var newRootPage = NewPage(tx, PageFlags.Branch, 1);
				cursor.Push(newRootPage);
				cursor.Root = newRootPage;
				cursor.Depth++;
				cursor.RecordNewPage(newRootPage, 1);

				// now add implicit left page
				newRootPage.AddNode(0, new Slice(SliceOptions.BeforeAllKeys), pageNumber: page.PageNumber);
				parentPage = newRootPage;
				parentPage.LastSearchPosition++;
			}
			else
			{
				// we already popped the page, so the current one on the stack is what the parent of the page
				parentPage = cursor.CurrentPage;
			}

			var splitIndex = (ushort)(page.NumberOfEntries / 2);
			if (currentIndex < splitIndex)
				newPosition = false;

			if (page.IsLeaf)
			{
				splitIndex = AdjustSplitPosition(newKey, value, page, currentIndex, splitIndex, ref newPosition);
			}

			// here we the current key is the separator key and can go either way, so 
			// use newPosition to decide if it stays on the left node or moves to the right
			Slice seperatorKey;
			if (currentIndex == splitIndex && newPosition)
			{
				seperatorKey = newKey;
			}
			else
			{
				var node = page.GetNode(splitIndex);
				seperatorKey = new Slice(node);
			}

			if (parentPage.SizeLeft < Util.GetBranchSize(seperatorKey) + Constants.NodeOffsetSize)
			{
				SplitPage(tx, seperatorKey, null, rightPage.PageNumber, cursor);
			}
			else
			{
				parentPage.AddNode(parentPage.LastSearchPosition, seperatorKey, pageNumber: rightPage.PageNumber);
			}
			// move the actual entries from page to right page
			var nKeys = page.NumberOfEntries;
			for (ushort i = splitIndex; i < nKeys; i++)
			{
				var node = page.GetNode(i);
				rightPage.CopyNodeData(node);
			}
			page.Truncate(tx, splitIndex);

			// actually insert the new key
			if (currentIndex >= splitIndex)
			{
				var pos = rightPage.NodePositionFor(newKey, _cmp);
				rightPage.AddNode(pos, newKey, value, pageNumber);
				cursor.Push(rightPage);
			}
			else
			{
				page.AddNode(page.LastSearchPosition,newKey, value);
				cursor.Push(page);
			}

			return parentPage;
		}

		/// <summary>
		/// For leaf pages, check the split point based on what
		///	fits where, since otherwise adding the node can fail.
		///	
		///	This check is only needed when the data items are
		///	relatively large, such that being off by one will
		///	make the difference between success or failure.
		///	
		///	It's also relevant if a page happens to be laid out
		///	such that one half of its nodes are all "small" and
		///	the other half of its nodes are "large." If the new
		///	item is also "large" and falls on the half with
		///	"large" nodes, it also may not fit.
		/// </summary>
		private static ushort AdjustSplitPosition(Slice key, Stream value, Page page, ushort currentIndex, ushort splitIndex,
													  ref bool newPosition)
		{
			var nodeSize = Util.GetLeafNodeSize(key, value);
			if (page.NumberOfEntries >= 20 && nodeSize <= Constants.PageMaxSpace / 16)
			{
				return splitIndex;
			}

			int pageSize = nodeSize;
			if (currentIndex <= splitIndex)
			{
				newPosition = false;
				for (int i = 0; i < splitIndex; i++)
				{
					var node = page.GetNode(i);
					pageSize += node->GetNodeSize();
					pageSize += pageSize & 1;
					if (pageSize > Constants.PageMaxSpace)
					{
						if (i <= currentIndex)
						{
							if (i < currentIndex)
								newPosition = true;
							return currentIndex;
						}
						return (ushort)i;
					}
				}
			}
			else
			{
				for (int i = page.NumberOfEntries - 1; i >= splitIndex; i--)
				{
					var node = page.GetNode(i);
					pageSize += node->GetNodeSize();
					pageSize += pageSize & 1;
					if (pageSize > Constants.PageMaxSpace)
					{
						if (i >= currentIndex)
						{
							newPosition = false;
							return currentIndex;
						}
						return (ushort)(i + 1);
					}
				}
			}
			return splitIndex;
		}

		public Page FindPageFor(Transaction tx, Slice key, Cursor cursor)
		{
			var p = cursor.Root;
			cursor.Push(p);
			while (p.Flags.HasFlag(PageFlags.Branch))
			{
				ushort nodePos;
				if (key.Options == SliceOptions.BeforeAllKeys)
				{
					nodePos = 0;
				}
				else if (key.Options == SliceOptions.AfterAllKeys)
				{
					nodePos = (ushort)(p.NumberOfEntries - 1);
				}
				else
				{
					int match;
					if (p.Search(key, _cmp, out match) != null)
					{
						nodePos = p.LastSearchPosition;
						if (match != 0)
							nodePos--;
					}
					else
					{
						nodePos = (ushort) (p.LastSearchPosition - 1);
					}

				}

				var node = p.GetNode(nodePos);
				p = tx.GetPage(node->PageNumber);
				cursor.Push(p);
			}

			if (p.IsLeaf == false)
				throw new DataException("Index points to a non leaf page");

			p.NodePositionFor(key, _cmp); // will set the LastSearchPosition

			return p;
		}

		private static Page NewPage(Transaction tx, PageFlags flags, int num)
		{
			var page = tx.AllocatePage(num);

			page.Flags = flags;

			return page;
		}
	}
}