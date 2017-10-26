﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Konamiman.SuperBookmarks
{
    partial class BookmarksManager
    {
        public SerializableBookmarksInfo GetSerializableInfo()
        {
            string RelativePath(string fullPath) =>
                fullPath.Substring(SolutionPath.Length);

            var filesWithBookmarks = new List<SerializableBookmarksInfo.FileWithBookmarks>();
            var lineNumbers = new List<int>();

            //We shouldn't have any duplicate bookmark, but let's play safe
            var usedLineNumbers = new List<int>();

            foreach (var fileName in viewsByFilename.Keys)
            {
                var view = viewsByFilename[fileName];
                if (bookmarksByView[view].Count == 0) continue;

                lineNumbers.Clear();
                usedLineNumbers.Clear();
                foreach (var bookmark in bookmarksByView[view])
                {
                    var lineNumber = bookmark.GetRow(Helpers.GetRootTextBuffer(view.TextBuffer));
                    if (usedLineNumbers.Contains(lineNumber))
                        continue;

                    lineNumbers.Add(lineNumber);
                    usedLineNumbers.Add(lineNumber);
                }

                filesWithBookmarks.Add(new SerializableBookmarksInfo.FileWithBookmarks
                {
                    FileName = RelativePath(fileName),
                    LinesWithBookmarks = lineNumbers.ToArray()
                });
            }

            foreach (var fileName in bookmarksPendingCreation.Keys)
            {
                filesWithBookmarks.Add(new SerializableBookmarksInfo.FileWithBookmarks
                {
                    FileName = RelativePath(fileName),
                    LinesWithBookmarks = bookmarksPendingCreation[fileName].ToArray()
                });
            }

            var info = new SerializableBookmarksInfo();
            var context = new SerializableBookmarksInfo.BookmarksContext
            {
                Name = "(default)",
                FilesWithBookmarks = filesWithBookmarks.ToArray()
            };
            info.BookmarksContexts = new[] { context };

            return info;
        }

        public void RecreateBookmarksFromSerializedInfo(SerializableBookmarksInfo info)
        {
            foreach (var item in info.BookmarksContexts[0].FilesWithBookmarks)
            {
                var fileName = Path.Combine(SolutionPath, item.FileName);
                if (!File.Exists(fileName)) continue;

                var lineNumbers = item.LinesWithBookmarks;

                if (viewsByFilename.ContainsKey(fileName))
                {
                    var view = viewsByFilename[fileName];
                    var buffer = view.TextBuffer;
                    var bookmarks = bookmarksByView[view];
                    foreach (var lineNumber in lineNumbers)
                    {
                        var bookmarkExists = bookmarks.Any(b => b.GetRow(buffer) == lineNumber);
                        if (bookmarkExists) continue;

                        var trackingSpan = Helpers.CreateTagSpan(buffer, lineNumber);
                        if (trackingSpan != null)
                        {
                            bookmarks.Add(new Bookmark(trackingSpan));
                            if (bookmarks.Count == 1)
                                SolutionExplorerFilter.OnFileGotItsFirstBookmark(fileName);
                        }
                    }
                }
                else if(bookmarksPendingCreation.ContainsKey(fileName))
                {
                    bookmarksPendingCreation[fileName] = bookmarksPendingCreation[fileName].Union(lineNumbers).ToArray();
                }
                else
                {
                    bookmarksPendingCreation.Add(fileName, lineNumbers);
                    SolutionExplorerFilter.OnFileGotItsFirstBookmark(fileName);
                }
            }
        }

        public PersistableBookmarksInfo GetPersistableInfo()
        {
            string RelativePath(string fullPath) =>
                fullPath.Substring(SolutionPath.Length);

            var info = new PersistableBookmarksInfo();

            //We shouldn't have any duplicate bookmark, but let's play safe
            var usedLineNumbers = new List<int>();

            foreach (var fileName in viewsByFilename.Keys)
            {
                var view = viewsByFilename[fileName];
                if (bookmarksByView[view].Count == 0) continue;

                var persistableBookmarks = new List<PersistableBookmarksInfo.Bookmark>();
                usedLineNumbers.Clear();
                foreach (var bookmark in bookmarksByView[view])
                {
                    var lineNumber = bookmark.GetRow(Helpers.GetRootTextBuffer(view.TextBuffer));
                    if (usedLineNumbers.Contains(lineNumber))
                        continue;

                    persistableBookmarks.Add(new PersistableBookmarksInfo.Bookmark
                    {
                        LineNumber = lineNumber
                    });
                    usedLineNumbers.Add(lineNumber);
                }
                info.BookmarksByFilename[RelativePath(fileName)] = persistableBookmarks.ToArray();
            }

            foreach (var filename in bookmarksPendingCreation.Keys)
            {
                var persistableBookmarks =
                    bookmarksPendingCreation[filename]
                    .Select(l => new PersistableBookmarksInfo.Bookmark { LineNumber = l });
                info.BookmarksByFilename[RelativePath(filename)] = persistableBookmarks.ToArray();
            }

            return info;
        }

        public void RecreateBookmarksFromPersistableInfo(PersistableBookmarksInfo info)
        {
            ClearAllBookmarks();

            foreach (var fileName in info.BookmarksByFilename.Keys)
            {
                var lineNumbers = info.BookmarksByFilename[fileName].Select(b => b.LineNumber).ToArray();
                bookmarksPendingCreation[Path.Combine(SolutionPath, fileName)] = lineNumbers;
                SolutionExplorerFilter.OnFileGotItsFirstBookmark(fileName);
            }
        }
    }
}
