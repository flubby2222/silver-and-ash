#!/usr/bin/env perl
# Replacement step of tools/gegenprobe.sh (issues #573, #604), on its own so tools/gegenprobe.tests.sh can check it
# without dotnet. Reads <file> byte for byte (CRLF stays) and writes the changed text to standard output, or with
# --count only the number of places <search> occurs. <search> and <replace> come from the environment variables
# GEGENPROBE_SEARCH and GEGENPROBE_REPLACE, so no shell quoting gets in the way.
#
# Usage: perl tools/gegenprobe-replace.pl [--regex] [--all] [--count] <file>
#
#   without --regex  <search> and <replace> are taken literally, every character
#   with --regex     <search> is a Perl regular expression; in <replace>, $1, $2 ... or ${1} insert its groups
#                    ($0 the whole match, a group that did not match gives empty text), \$ writes a plain $ and
#                    \\ a plain \. Every other backslash stays as it is (a "\n" in C# stays "\n").
#   --all            change every place (default: only the first one)
#
# Exit code 0, or 2 with a message (unknown option, <replace> uses a group <search> does not have).

use strict;
use warnings;

my ($regex, $all, $count) = (0, 0, 0);
while (@ARGV && $ARGV[0] =~ /^--/) {
    my $option = shift @ARGV;
    if    ($option eq '--regex') { $regex = 1 }
    elsif ($option eq '--all')   { $all = 1 }
    elsif ($option eq '--count') { $count = 1 }
    else  { fail("unknown option $option") }
}
fail("need [--regex] [--all] [--count] <file>") unless @ARGV == 1;
my $file = $ARGV[0];
my $search = $ENV{GEGENPROBE_SEARCH};
my $replace = $ENV{GEGENPROBE_REPLACE};
fail("GEGENPROBE_SEARCH is not set") unless defined $search;

open my $in, '<:raw', $file or fail("cannot read $file: $!");
my $text = do { local $/; <$in> };
close $in;
$text = '' unless defined $text;

my $pattern = $regex ? qr/$search/ : qr/\Q$search\E/;

if ($count) {
    my $hits = () = $text =~ /$pattern/g;
    print "$hits";
    exit 0;
}

fail("GEGENPROBE_REPLACE is not set") unless defined $replace;
if ($all) {
    $text =~ s/$pattern/$regex ? fill($replace) : $replace/ge;
} else {
    $text =~ s/$pattern/$regex ? fill($replace) : $replace/e;
}
binmode STDOUT;
print $text;
exit 0;

# Puts the groups of the match that is being replaced into <replace>. The groups are read first, before the inner
# substitution below starts a match of its own.
sub fill {
    my ($template) = @_;
    my @groups = ($&, @{^CAPTURE});
    my $last = $#+;    # groups in <search>, also those that did not take part in this match
    $#groups = $last if $#groups < $last;
    $template =~ s{\\([\\\$])|\$\{(\d+)\}|\$(\d+)}{defined $1 ? $1 : group(\@groups, defined $2 ? $2 : $3)}ge;
    return $template;
}

sub group {
    my ($groups, $n) = @_;
    fail("<replace> uses \$$n, but <search> has only " . $#$groups . " group(s)") if $n > $#$groups;
    return defined $groups->[$n] ? $groups->[$n] : '';
}

sub fail {
    my ($message) = @_;
    print STDERR "gegenprobe: $message\n";
    exit 2;
}
